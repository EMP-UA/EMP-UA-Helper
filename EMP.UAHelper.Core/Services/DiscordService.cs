// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для відправки сповіщень у Discord через вебхук
// EN: Service for sending notifications to Discord via webhook
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;
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

        // UA: Кольори embed повідомлень для різних типів контенту (десяткове RGB)
        // EN: Embed message colors for different content types (decimal RGB)
        private const int ColorLive = 0xFF0000; // UA: Червоний / EN: Red
        private const int ColorUpcoming = 0xFFA500; // UA: Помаранчевий / EN: Orange
        private const int ColorVideo = 0x8A46C1; // UA: Фіолетовий EMP / EN: EMP Purple
        private const int ColorShort = 0xC989F3; // UA: Світло-фіолетовий EMP / EN: EMP Light Purple

        public DiscordService(string webhookUrl, string twitchUrl, string roleId, TemplateService templateService)
        {
            _webhookUrl = webhookUrl;
            _twitchUrl = twitchUrl;
            _roleId = roleId;
            _templateService = templateService;
        }

        // UA: Відправити embed сповіщення залежно від типу контенту
        // EN: Send embed notification depending on content type
        public async Task SendNotificationAsync(VideoInfo video)
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
                video.ScheduledStartTime);

            var description = _templateService.Apply(
                bodyTemplate,
                video.Title,
                video.Url,
                _twitchUrl,
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
            if (!string.IsNullOrEmpty(_roleId))
                payload["content"] = $"<@&{_roleId}>";

            var json = JsonSerializer.Serialize(payload);
            using var httpClient = new HttpClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await httpClient.PostAsync(_webhookUrl, content);
        }
    }
}