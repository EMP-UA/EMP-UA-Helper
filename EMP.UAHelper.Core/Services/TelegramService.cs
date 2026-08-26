// =============================================================================
// EMP UA Helper — TelegramService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для відправки сповіщень у Telegram канал
// EN: Service for sending notifications to a Telegram channel
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace EMP.UAHelper.Core.Services
{
    public class TelegramService
    {
        private readonly TelegramBotClient _botClient;
        private readonly string _channelUsername;
        private readonly string _twitchUrl;
        private readonly TemplateService _templateService;

        // UA: Резолвиться один раз при побудові сервісу (через AppTimeZone,
        //     без хардкоду) — сервіс і так перестворюється при кожній зміні
        //     налаштувань (див. ContentDispatchFactory), тож "застаріла" зона
        //     тут неможлива
        // EN: Resolved once when the service is built (via AppTimeZone, no
        //     hardcoding) — the service is already recreated on every
        //     settings change (see ContentDispatchFactory), so a "stale"
        //     zone here isn't possible
        private readonly TimeZoneInfo _timeZone;

        public TelegramService(string botToken, string channelUsername, string twitchUrl,
            string timeZoneId, TemplateService templateService)
        {
            _botClient = new TelegramBotClient(botToken);
            _channelUsername = channelUsername;
            _twitchUrl = twitchUrl;
            _timeZone = AppTimeZone.Resolve(timeZoneId);
            _templateService = templateService;
        }

        // UA: Відправити сповіщення залежно від типу контенту
        // EN: Send notification depending on content type
        public async Task SendNotificationAsync(VideoInfo video)
        {
            var templates = _templateService.GetTemplates().Telegram;

            // UA: Обираємо шаблон залежно від типу контенту
            // EN: Select template based on content type
            var template = video.Type switch
            {
                VideoType.Live => templates.Live,
                VideoType.Upcoming => templates.Upcoming,
                VideoType.Video => templates.Video,
                VideoType.Short => templates.Short,
                _ => throw new ArgumentOutOfRangeException()
            };

            // UA: Передаємо ScheduledStartTime для підстановки дати у шаблон
            // EN: Pass ScheduledStartTime for date substitution in template
            var text = _templateService.Apply(
                template,
                video.Title,
                video.Url,
                _twitchUrl,
                _timeZone,
                video.ScheduledStartTime);

            await _botClient.SendMessage(
                chatId: _channelUsername,
                text: text,
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions
                {
                    IsDisabled = false,
                    Url = video.Url,
                    PreferLargeMedia = true,
                    ShowAboveText = true
                }
            );
        }

        // UA: Повністю ручна відправка — без TemplateService, текст іде як є.
        //     ParseMode.Html залишаємо увімкненим, щоб користувач міг сам
        //     вручну вписати HTML-теги Telegram (<b>, <i>, <a href>, <code>,
        //     <tg-spoiler> тощо) — якщо тегів немає, звичайний текст
        //     надсилається без змін. Прев'ю посилання лишаємо на розсуд
        //     Telegram (не примушуємо конкретний URL, як у шаблонному режимі)
        // EN: Fully manual send — bypasses TemplateService, text goes as-is.
        //     ParseMode.Html is kept on so the user can hand-write Telegram
        //     HTML tags (<b>, <i>, <a href>, <code>, <tg-spoiler>, etc.) — if
        //     no tags are present, plain text is sent unchanged. Link preview
        //     is left to Telegram's own detection (we don't force a specific
        //     URL like in template mode)
        //     imagePaths — необов'язкові локальні файли зображень (ручний
        //     режим). 1 файл іде через sendPhoto з підписом; 2-10 — через
        //     sendMediaGroup, де Telegram вимагає підпис лише на одному
        //     елементі масиву (беремо перший — решта підпису не показують).
        //     null/порожній список — той самий sendMessage, що й раніше.
        //     imagePaths — optional local image files (manual mode). 1 file
        //     goes through sendPhoto with a caption; 2-10 go through
        //     sendMediaGroup, where Telegram requires the caption on exactly
        //     one array item (we use the first — the rest show no caption).
        //     null/empty means the same sendMessage as before.
        public async Task SendRawAsync(string text, IReadOnlyList<string>? imagePaths = null)
        {
            var hasImages = imagePaths is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(text) && !hasImages) return;

            if (!hasImages)
            {
                await _botClient.SendMessage(
                    chatId: _channelUsername,
                    text: text,
                    parseMode: ParseMode.Html
                );
                return;
            }

            if (imagePaths!.Count == 1)
            {
                await using var stream = File.OpenRead(imagePaths[0]);
                await _botClient.SendPhoto(
                    chatId: _channelUsername,
                    photo: InputFile.FromStream(stream, Path.GetFileName(imagePaths[0])),
                    caption: text,
                    parseMode: ParseMode.Html
                );
                return;
            }

            // UA: 22.10.1 підтверджено через метадані складання — sendPhoto й
            //     sendMediaGroup саме з такими параметрами, InputFile.FromStream
            //     і InputMediaPhoto.Caption/ParseMode існують у цій версії
            //     Telegram.Bot (dotnet SDK в пісочниці відсутній, тому
            //     перевірено напряму по IL-метаданих DLL, а не з документації)
            // EN: Confirmed against the assembly's own metadata for 22.10.1 —
            //     sendPhoto and sendMediaGroup have exactly these parameters,
            //     and InputFile.FromStream / InputMediaPhoto.Caption/ParseMode
            //     exist in this Telegram.Bot version (no dotnet SDK in the
            //     sandbox, so this was verified directly against the DLL's IL
            //     metadata rather than from documentation)
            var files = imagePaths.Take(MaxAttachmentsPerMessage).ToList();
            var streams = files.Select(File.OpenRead).ToList();
            try
            {
                var media = new List<InputMediaPhoto>();
                for (var i = 0; i < files.Count; i++)
                {
                    var item = new InputMediaPhoto(InputFile.FromStream(streams[i], Path.GetFileName(files[i])));
                    if (i == 0)
                    {
                        item.Caption = text;
                        item.ParseMode = ParseMode.Html;
                    }

                    media.Add(item);
                }

                await _botClient.SendMediaGroup(chatId: _channelUsername, media: media);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        // UA: Telegram дозволяє щонайбільше 10 елементів у медіа-групі — це
        //     жорсткий ліміт самого API, не вибір UI (на відміну від
        //     аналогічної константи в DiscordService)
        // EN: Telegram allows at most 10 items in a media group — this is a
        //     hard API limit, not a UI choice (unlike the equivalent constant
        //     in DiscordService)
        public const int MaxAttachmentsPerMessage = 10;
    }
}