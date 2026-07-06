// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Фабрика — будує YouTubeService (опційно) і NotificationDispatcher
//     з поточних AppSettings. Викликається як при старті програми, так і
//     повторно з вікна налаштувань — щоб змінити комбінацію платформ
//     "сьогодні" без перезапуску
// EN: Factory — builds an optional YouTubeService and a NotificationDispatcher
//     from the current AppSettings. Called both at app startup and again from
//     the settings window — to change "today's" platform combination
//     without restarting
namespace EMP.UAHelper.Core.Services
{
    public static class ContentDispatchFactory
    {
        public static (YouTubeService? YouTube, NotificationDispatcher Dispatcher) Build(
            AppSettings settings,
            TemplateService templateService,
            CrashLogService crashLogService)
        {
            // UA: Якщо Twitch вимкнено — передаємо порожній рядок,
            //     щоб TemplateService.Apply прибрав відповідні рядки з шаблонів
            // EN: If Twitch is disabled — pass an empty string so
            //     TemplateService.Apply strips the corresponding template lines
            var twitchUrl = settings.UseTwitch ? settings.TwitchUrl : string.Empty;

            var youTubeService = settings.UseYouTube
                ? new YouTubeService(settings.YoutubeApiKey, settings.ChannelId)
                : null;

            var telegramService = settings.UseTelegram
                ? new TelegramService(
                    settings.TelegramBotToken,
                    settings.ChannelUsername,
                    twitchUrl,
                    templateService)
                : null;

            var discordService = settings.UseDiscord
                ? new DiscordService(
                    settings.DiscordWebhookUrl,
                    twitchUrl,
                    settings.DiscordRoleId,
                    templateService)
                : null;

            var dispatcher = new NotificationDispatcher(telegramService, discordService, crashLogService);

            return (youTubeService, dispatcher);
        }
    }
}