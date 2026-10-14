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

        // UA: Резолвиться один раз під час побудови сервісу (через AppTimeZone,
        //     без хардкоду) — сервіс і так перестворюється щоразу, коли
        //     змінюються налаштування (див. ContentDispatchFactory), тож
        //     "застаріла" зона тут неможлива
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

        // UA: Відправити сповіщення залежно від типу контенту.
        //     Повертає ID відправленого повідомлення Telegram як рядок —
        //     підтвердження реальної відправки. На відміну від Discord-вебхука,
        //     Telegram Bot API синхронний: якщо await завершився без винятку
        //     (напр. ApiRequestException при 429 "Too Many Requests"), повідомлення
        //     вже гарантовано доставлене — тут достатньо просто повернути MessageId.
        // EN: Send notification depending on content type.
        //     Returns the sent Telegram message's ID as a string — confirmation
        //     of an actual send. Unlike Discord's webhook, the Telegram Bot API
        //     is synchronous: if the await completes without throwing (e.g. an
        //     ApiRequestException on a 429 "Too Many Requests"), the message is
        //     already guaranteed delivered — just returning MessageId is enough.
        public async Task<string> SendNotificationAsync(VideoInfo video)
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

            var message = await _botClient.SendMessage(
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

            return message.MessageId.ToString();
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
        //     Повертає ID(и) надісланого повідомлення (чи кількох — для медіа-
        //     групи) як рядок, або null, якщо не було ні тексту, ні зображень
        //     (відправка взагалі не відбувалась).
        //     Returns the sent message's ID(s) (several, for a media group) as
        //     a string, or null if there was neither text nor images (nothing
        //     was sent at all).
        public async Task<string?> SendRawAsync(string text, IReadOnlyList<string>? imagePaths = null)
        {
            var hasImages = imagePaths is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(text) && !hasImages) return null;

            if (!hasImages)
            {
                var sent = await _botClient.SendMessage(
                    chatId: _channelUsername,
                    text: text,
                    parseMode: ParseMode.Html
                );
                return sent.MessageId.ToString();
            }

            if (imagePaths!.Count == 1)
            {
                await using var stream = File.OpenRead(imagePaths[0]);
                var sent = await _botClient.SendPhoto(
                    chatId: _channelUsername,
                    photo: InputFile.FromStream(stream, Path.GetFileName(imagePaths[0])),
                    caption: text,
                    parseMode: ParseMode.Html
                );
                return sent.MessageId.ToString();
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

            // UA: Потоки відкриваються ОДИН ЗА ОДНИМ усередині try, а не разом
            //     через .Select(File.OpenRead).ToList() до try — інакше, якщо
            //     відкриття, скажімо, третього файлу кине виняток (видалили з
            //     диска між вибором у галереї й відправкою, немає прав
            //     доступу), два вже відкриті дескриптори першого й другого
            //     файлів ніхто не закриє: виняток стався ще ДО входу в
            //     try/finally, що їх звільняє.
            // EN: Streams are opened ONE AT A TIME inside the try, not all at
            //     once via .Select(File.OpenRead).ToList() before the try —
            //     otherwise, if opening, say, the third file throws (removed
            //     from disk between the gallery pick and sending, no access
            //     rights), the two already-open handles for the first and
            //     second files are never released: the exception happens
            //     BEFORE entering the try/finally that disposes them.
            var streams = new List<FileStream>();
            try
            {
                foreach (var file in files)
                    streams.Add(File.OpenRead(file));

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

                var sent = await _botClient.SendMediaGroup(chatId: _channelUsername, media: media);
                return string.Join(",", sent.Select(m => m.MessageId));
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

        // UA: Перевірка з'єднання БЕЗ фактичної відправки — щоб проблему зі
        //     з'єднанням було видно ще до натискання "Надіслати", а не
        //     постфактум. GetMe() підтверджує, що токен бота взагалі
        //     дійсний; GetChat() окремо підтверджує, що саме ЦЕЙ бот бачить
        //     саме ЦЕЙ канал (ловить, наприклад, друкарську помилку в
        //     username каналу — валідний токен тут нічого не гарантує).
        //     Обидва виклики — легкі, лише читають інформацію, нічого не
        //     публікують і не видно жодному підписнику каналу.
        // EN: A connection check WITHOUT an actual send — so a connectivity
        //     problem is visible before "Send" is pressed, not after the
        //     fact. GetMe() confirms the bot token is valid at all; GetChat()
        //     separately confirms THIS bot can see THIS channel (catches,
        //     say, a typo in the channel username — a valid token guarantees
        //     nothing there). Both calls are lightweight, read-only, post
        //     nothing, and are invisible to any channel subscriber.
        public async Task<(bool Ok, string? Error)> CheckConnectionAsync()
        {
            try
            {
                await _botClient.GetMe();
            }
            catch (Exception ex)
            {
                return (false, $"Telegram: {ex.Message}");
            }

            try
            {
                await _botClient.GetChat(_channelUsername);
            }
            catch (Exception ex)
            {
                return (false, $"Telegram ({_channelUsername}): {ex.Message}");
            }

            return (true, null);
        }
    }
}