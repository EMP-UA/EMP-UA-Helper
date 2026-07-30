// =============================================================================
// EMP UA Helper — DiscordService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для відправки сповіщень у Discord через вебхук
// EN: Service for sending notifications to Discord via webhook
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EMP.UAHelper.Core.Services
{
    public class DiscordService
    {
        private readonly string _webhookUrl;
        private readonly string _twitchUrl;
        private readonly string _roleId;
        private readonly TemplateService _templateService;

        // UA: Див. коментар до аналогічного поля в TelegramService — той
        //     самий підхід, той самий AppTimeZone, без хардкоду
        // EN: See the comment on the equivalent field in TelegramService —
        //     same approach, same AppTimeZone, no hardcoding
        private readonly TimeZoneInfo _timeZone;

        // UA: Формати згадок Discord. Винесені сюди, бо синтаксис у трьох
        //     типів різний рівно однією позначкою ("&" і "#"), і саме на цьому
        //     найлегше помилитись, набираючи вручну. Тримати їх в одному місці
        //     означає, що і генератор у вікні надсилання, і автоматичний пінг
        //     ролі нижче користуються тим самим кодом.
        // EN: Discord mention formats. Kept here because the syntax of the
        //     three types differs by exactly one character ("&" and "#"), which
        //     is precisely what's easiest to get wrong when typing by hand.
        //     Having them in one place means both the generator in the send
        //     window and the automatic role ping below use the same code.
        public enum MentionKind
        {
            Role,
            User,
            Channel
        }

        // UA: Побудувати тег згадки з "голого" ID. Повертає порожній рядок для
        //     порожнього/некоректного ID, щоб у повідомлення не потрапив
        //     обрізаний тег на кшталт "<@&>", який Discord покаже як текст
        // EN: Build a mention tag from a "bare" ID. Returns an empty string for
        //     an empty/invalid ID, so no truncated tag like "<@&>" — which
        //     Discord would render as plain text — ends up in the message
        public static string FormatMention(MentionKind kind, string? id)
        {
            var trimmed = id?.Trim() ?? string.Empty;

            // UA: ID у Discord — це snowflake, тобто виключно цифри ASCII 0-9.
            //     Перевірка дешева, а рятує від найтиповішого випадку: людина
            //     копіює з Discord готовий тег "<@&123>" замість самого ID, і
            //     без перевірки ми б згенерували "<@&<@&123>>".
            //     Саме IsAsciiDigit, а не IsDigit: IsDigit вважає цифрою будь-який
            //     символ Unicode-категорії Nd, тобто пропустив би, наприклад,
            //     арабо-індійські "١٢٣" — вони виглядають як число, але Discord
            //     такий тег не розпізнає і покаже його як звичайний текст.
            // EN: A Discord ID is a snowflake, i.e. ASCII digits 0-9 only. The
            //     check is cheap and guards the most typical case: the person
            //     copies a ready tag "<@&123>" out of Discord instead of the
            //     bare ID, and without the check we'd generate "<@&<@&123>>".
            //     IsAsciiDigit specifically, not IsDigit: IsDigit treats any
            //     Unicode category Nd character as a digit, so it would let
            //     through e.g. Arabic-Indic "١٢٣" — which looks like a number
            //     but Discord won't recognize the tag and will render it as text.
            if (trimmed.Length == 0 || !trimmed.All(char.IsAsciiDigit))
                return string.Empty;

            return kind switch
            {
                MentionKind.Role => $"<@&{trimmed}>",
                MentionKind.User => $"<@{trimmed}>",
                MentionKind.Channel => $"<#{trimmed}>",
                _ => string.Empty
            };
        }

        // UA: Кольори embed повідомлень для різних типів контенту (десяткове RGB)
        // EN: Embed message colors for different content types (decimal RGB)
        private const int ColorLive = 0xFF0000; // UA: Червоний / EN: Red
        private const int ColorUpcoming = 0xFFA500; // UA: Помаранчевий / EN: Orange
        private const int ColorVideo = 0x8A46C1; // UA: Фіолетовий EMP / EN: EMP Purple
        private const int ColorShort = 0xC989F3; // UA: Світло-фіолетовий EMP / EN: EMP Light Purple

        public DiscordService(string webhookUrl, string twitchUrl, string roleId,
            string timeZoneId, TemplateService templateService)
        {
            _webhookUrl = webhookUrl;
            _twitchUrl = twitchUrl;
            _roleId = roleId;
            _timeZone = AppTimeZone.Resolve(timeZoneId);
            _templateService = templateService;
        }

        // UA: Відправити embed сповіщення залежно від типу контенту
        // EN: Send embed notification depending on content type
        //     webhookUrl — необов'язкове перевизначення каналу. Порожнє
        //     значення означає "канал за замовчуванням з налаштувань", тому
        //     наявні виклики (автоматичні сповіщення) працюють без змін.
        //     webhookUrl — an optional channel override. An empty value means
        //     "the default channel from settings", so existing call sites
        //     (automatic notifications) keep working unchanged.
        public async Task SendNotificationAsync(VideoInfo video, string? webhookUrl = null)
        {
            var templates = _templateService.GetTemplates();
            var bodyTemplates = templates.Discord;
            var titleTemplates = templates.DiscordTitles;

            // UA: Обираємо шаблони залежно від типу контенту
            // EN: Select templates based on content type
            var (titleTemplate, bodyTemplate, color) = video.Type switch
            {
                VideoType.Live =>
                    (titleTemplates.Live, bodyTemplates.Live, ColorLive),
                VideoType.Upcoming =>
                    (titleTemplates.Upcoming, bodyTemplates.Upcoming, ColorUpcoming),
                VideoType.Video =>
                    (titleTemplates.Video, bodyTemplates.Video, ColorVideo),
                VideoType.Short =>
                    (titleTemplates.Short, bodyTemplates.Short, ColorShort),
                _ => throw new ArgumentOutOfRangeException()
            };

            // UA: Передаємо ScheduledStartTime для підстановки Unix timestamp у шаблон
            // EN: Pass ScheduledStartTime for Unix timestamp substitution in template
            var embedTitle = _templateService.Apply(
                titleTemplate,
                video.Title,
                video.Url,
                _twitchUrl,
                _timeZone,
                video.ScheduledStartTime);

            var description = _templateService.Apply(
                bodyTemplate,
                video.Title,
                video.Url,
                _twitchUrl,
                _timeZone,
                video.ScheduledStartTime);

            // UA: Embed будуємо через Dictionary, щоб мати змогу умовно пропустити
            //     image, якщо превʼю немає (ручне сповіщення без YouTube)
            // EN: Build the embed via Dictionary so we can conditionally skip
            //     the image when there's no thumbnail (manual notification without YouTube)
            var embed = new Dictionary<string, object>
            {
                ["title"] = embedTitle,
                ["description"] = description,
                ["color"] = color,
                // UA: Підпис embed в тематиці EMP
                // EN: Embed footer in EMP theme
                ["footer"] = new { text = "Silence will fall." }
            };

            if (!string.IsNullOrEmpty(video.ThumbnailUrl))
                embed["image"] = new { url = video.ThumbnailUrl };

            var payload = new Dictionary<string, object>
            {
                ["embeds"] = new[] { embed }
            };

            // UA: Пінг ролі додаємо лише якщо RoleId заданий
            // EN: Add the role mention only if RoleId is set
            var roleMention = FormatMention(MentionKind.Role, _roleId);
            if (roleMention.Length > 0)
                payload["content"] = roleMention;

            await PostAsync(payload, webhookUrl);
        }

        // UA: Повністю ручна відправка — без TemplateService і без embed,
        //     звичайне повідомлення ("content"). Discord сам розпізнає
        //     markdown-форматування (**жирний**, *курсив*, __підкреслення__,
        //     ~~закреслення~~, `код`) і теги (<@&roleId> — роль, <@userId> —
        //     користувач, <#channelId> — канал, @everyone/@here), якщо вони
        //     присутні прямо в тексті — жодного додаткового коду для цього не
        //     потрібно. Ліміт Discord на довжину content — 2000 символів
        // EN: Fully manual send — bypasses TemplateService and the embed,
        //     a plain message ("content"). Discord natively recognizes
        //     markdown formatting (**bold**, *italic*, __underline__,
        //     ~~strikethrough~~, `code`) and tags (<@&roleId> — role,
        //     <@userId> — user, <#channelId> — channel, @everyone/@here) when
        //     they're present directly in the text — no extra code needed.
        //     Discord's content length limit is 2000 characters
        public async Task SendRawAsync(string text, string? webhookUrl = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var payload = new Dictionary<string, object> { ["content"] = text };
            await PostAsync(payload, webhookUrl);
        }

        // UA: Єдина точка відправки в Discord. Раніше і embed, і ручне
        //     повідомлення дублювали серіалізацію та створення HttpClient —
        //     тепер вибір каналу (перевизначення чи типовий) реалізований
        //     рівно один раз, тож ці два шляхи не можуть розійтись у поведінці.
        //     HttpClient статичний з тієї ж причини, що й у YouTubeService:
        //     новий екземпляр на кожну відправку лишає TCP-з'єднання висіти.
        // EN: The single Discord send point. Previously both the embed and the
        //     manual message duplicated serialization and HttpClient creation —
        //     now channel selection (override or default) is implemented
        //     exactly once, so the two paths can't drift apart in behavior.
        //     The HttpClient is static for the same reason as in YouTubeService:
        //     a new instance per send leaves TCP connections lingering.
        private static readonly HttpClient Http = new();

        private async Task PostAsync(Dictionary<string, object> payload, string? webhookUrl)
        {
            var target = string.IsNullOrWhiteSpace(webhookUrl) ? _webhookUrl : webhookUrl;
            if (string.IsNullOrWhiteSpace(target)) return;

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await Http.PostAsync(target, content);
        }
    }
}