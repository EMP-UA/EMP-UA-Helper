// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для відправки сповіщень у Telegram канал
// EN: Service for sending notifications to a Telegram channel
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

        public TelegramService(string botToken, string channelUsername, string twitchUrl, TemplateService templateService)
        {
            _botClient = new TelegramBotClient(botToken);
            _channelUsername = channelUsername;
            _twitchUrl = twitchUrl;
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

            var text = _templateService.Apply(template, video.Title, video.Url, _twitchUrl);

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
    }
}