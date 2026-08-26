// =============================================================================
// EMP UA Helper — NotificationDispatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Єдина точка відправки сповіщень — не залежить від джерела VideoInfo
//     (YouTube-автовиявлення чи ручний ввід дають той самий результат)
// EN: Single dispatch point for notifications — independent of the VideoInfo
//     source (YouTube auto-detection or manual entry produce the same result)
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;

namespace EMP.UAHelper.Core.Services
{
    public class NotificationDispatcher
    {
        private readonly TelegramService? _telegramService;
        private readonly DiscordService? _discordService;
        private readonly CrashLogService _crashLogService;

        public NotificationDispatcher(
            TelegramService? telegramService,
            DiscordService? discordService,
            CrashLogService crashLogService)
        {
            _telegramService = telegramService;
            _discordService = discordService;
            _crashLogService = crashLogService;
        }

        // UA: Розсилає на всі увімкнені платформи паралельно
        // EN: Sends to all enabled platforms in parallel
        //     discordWebhookUrl — перевизначення Discord-каналу для цієї
        //     конкретної відправки. null означає "канал за замовчуванням", тому
        //     автоматичні сповіщення (де вибору каналу немає) не змінюються.
        //     discordWebhookUrl — a Discord channel override for this specific
        //     send. null means "the default channel", so automatic
        //     notifications (which have no channel picker) are unaffected.
        public async Task SendAsync(VideoInfo video, string? discordWebhookUrl = null)
        {
            var urlLabel = string.IsNullOrEmpty(video.Url) ? "no-url" : video.Url;
            await _crashLogService.LogInfoAsync($"[{video.Type}] {video.Title} ({urlLabel})");

            var tasks = new List<Task>();
            if (_telegramService != null)
                tasks.Add(_telegramService.SendNotificationAsync(video));
            if (_discordService != null)
                tasks.Add(_discordService.SendNotificationAsync(video, discordWebhookUrl));

            await Task.WhenAll(tasks);
        }

        // UA: Розсилка у повністю ручному режимі — без шаблону, кожна
        //     платформа отримує свій текст незалежно. Порожній/пробільний
        //     текст для платформи означає "не відправляти туди" — це
        //     навмисно (наприклад, коли анонс потрібен лише в Discord)
        // EN: Fully manual dispatch — no template, each platform gets its
        //     own text independently. An empty/whitespace text for a
        //     platform means "don't send there" — this is intentional (e.g.
        //     when the announcement is only needed on Discord)
        //     imagePaths — необов'язкові локальні зображення, спільні для
        //     обох платформ (ручний режим). Правило "порожній текст = не
        //     відправляти туди" лишається чинним і для зображень — вкладення
        //     їдуть лише разом із текстом, а не замість нього. Це свідомо:
        //     інакше порожнє поле Telegram/Discord перестало б означати
        //     "сюди не надсилати", якщо галерея не порожня.
        //     imagePaths — optional local images shared by both platforms
        //     (manual mode). The "empty text = don't send there" rule still
        //     applies with images — attachments ride along with the text,
        //     they don't replace it. This is deliberate: otherwise an empty
        //     Telegram/Discord field would stop meaning "don't send here"
        //     whenever the gallery isn't empty.
        public async Task SendRawAsync(string? telegramText, string? discordText,
                                       string? discordWebhookUrl = null,
                                       IReadOnlyList<string>? imagePaths = null)
        {
            await _crashLogService.LogInfoAsync(
                $"[RAW] telegram={(string.IsNullOrWhiteSpace(telegramText) ? "no" : "yes")}, discord={(string.IsNullOrWhiteSpace(discordText) ? "no" : "yes")}, images={imagePaths?.Count ?? 0}");

            var tasks = new List<Task>();
            if (_telegramService != null && !string.IsNullOrWhiteSpace(telegramText))
                tasks.Add(_telegramService.SendRawAsync(telegramText, imagePaths));
            if (_discordService != null && !string.IsNullOrWhiteSpace(discordText))
                tasks.Add(_discordService.SendRawAsync(discordText, discordWebhookUrl, imagePaths));

            await Task.WhenAll(tasks);
        }
    }
}