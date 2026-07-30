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
        public async Task SendRawAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            await _botClient.SendMessage(
                chatId: _channelUsername,
                text: text,
                parseMode: ParseMode.Html
            );
        }
    }
}