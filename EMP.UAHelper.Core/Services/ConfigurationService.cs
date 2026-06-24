// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для читання конфігурації з appsettings.json
// EN: Service for reading configuration from appsettings.json
using Microsoft.Extensions.Configuration;

namespace EMP.UAHelper.Core.Services
{
    public class AppSettings
    {
        // UA: Токен Telegram бота
        // EN: Telegram bot token
        public string TelegramBotToken { get; set; } = string.Empty;

        // UA: Ключ YouTube Data API
        // EN: YouTube Data API key
        public string YoutubeApiKey { get; set; } = string.Empty;

        // UA: URL Discord вебхука
        // EN: Discord webhook URL
        public string DiscordWebhookUrl { get; set; } = string.Empty;

        // UA: ID ролі Discord для пінгу
        // EN: Discord role ID for mention
        public string DiscordRoleId { get; set; } = string.Empty;

        // UA: ID YouTube каналу
        // EN: YouTube channel ID
        public string ChannelId { get; set; } = string.Empty;

        // UA: Username Telegram каналу
        // EN: Telegram channel username
        public string ChannelUsername { get; set; } = string.Empty;

        // UA: URL Twitch каналу
        // EN: Twitch channel URL
        public string TwitchUrl { get; set; } = string.Empty;

        // UA: Мова інтерфейсу (uk/en)
        // EN: UI language (uk/en)
        public string? UiLanguage { get; set; }
    }

    public class ConfigurationService
    {
        // UA: Завантажені налаштування
        // EN: Loaded settings
        public AppSettings Settings { get; private set; }

        public ConfigurationService()
        {
            // UA: Шукаємо appsettings.json поруч з exe
            // EN: Looking for appsettings.json next to the exe
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            Settings = new AppSettings();
            config.Bind(Settings);
        }
    }
}