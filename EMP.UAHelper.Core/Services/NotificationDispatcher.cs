// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Єдина точка відправки сповіщень — не залежить від джерела VideoInfo
//     (YouTube-автовиявлення чи ручний ввід дають той самий результат)
// EN: Single dispatch point for notifications — independent of the VideoInfo
//     source (YouTube auto-detection or manual entry produce the same result)
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
        public async Task SendAsync(VideoInfo video)
        {
            var urlLabel = string.IsNullOrEmpty(video.Url) ? "no-url" : video.Url;
            await _crashLogService.LogInfoAsync($"[{video.Type}] {video.Title} ({urlLabel})");

            var tasks = new List<Task>();
            if (_telegramService != null)
                tasks.Add(_telegramService.SendNotificationAsync(video));
            if (_discordService != null)
                tasks.Add(_discordService.SendNotificationAsync(video));

            await Task.WhenAll(tasks);
        }
    }
}