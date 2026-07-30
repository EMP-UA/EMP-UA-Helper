// =============================================================================
// EMP UA Helper — ConfigurationService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для читання конфігурації з appsettings.json
// EN: Service for reading configuration from appsettings.json
// =============================================================================
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

        // UA: Чи використовувати Telegram (дефолт true для сумісності зі старими appsettings.json)
        // EN: Whether to use Telegram (defaults to true for backward compatibility with old appsettings.json)
        public bool UseTelegram { get; set; } = true;

        // UA: Чи використовувати Discord
        // EN: Whether to use Discord
        public bool UseDiscord { get; set; } = true;

        // UA: Чи використовувати YouTube як джерело автовиявлення контенту
        // EN: Whether to use YouTube as the auto-detection content source
        public bool UseYouTube { get; set; } = true;

        // UA: Чи додавати посилання на Twitch у шаблони
        // EN: Whether to include the Twitch link in templates
        public bool UseTwitch { get; set; } = true;

        // UA: Мова інтерфейсу (uk/en)
        // EN: UI language (uk/en)
        public string? UiLanguage { get; set; }

        // UA: Часова зона (Windows TZ ID, напр. "FLE Standard Time") для
        //     інтерпретації дати/часу трансляції в усіх вікнах програми.
        //     Порожньо = старий appsettings.json без цього поля — резолвиться
        //     в Київ через AppTimeZone.Resolve, щоб не міняти поведінку для
        //     вже наявних інсталяцій. При першому запуску вікно FirstRun
        //     автоматично підставляє сюди системну зону
        // EN: Timezone (Windows TZ ID, e.g. "FLE Standard Time") used to
        //     interpret stream date/time across the app. Empty = an old
        //     appsettings.json without this field — resolves to Kyiv via
        //     AppTimeZone.Resolve, so behavior for existing installs doesn't
        //     change. On first run, the FirstRun window auto-fills this with
        //     the system's own zone
        public string TimeZoneId { get; set; } = string.Empty;

        // UA: Відображувана назва основного Discord-каналу. Порожньо =
        //     показуємо локалізовану підпис за замовчуванням, тож нікого не
        //     змушуємо вигадувати назву, якщо канал один
        // EN: Display name of the primary Discord channel. Empty = show the
        //     localized default label, so nobody is forced to invent a name
        //     when they only have one channel
        public string DiscordWebhookName { get; set; } = string.Empty;

        // UA: Додаткові іменовані Discord-канали. Назви задає користувач, а не
        //     ми: жорстко зашити "для розкладу"/"для інших цілей" означало б
        //     нав'язати всім свій сценарій використання — у когось це буде
        //     "тест", "англомовний канал" чи "модератори". Список порожній за
        //     замовчуванням, тож для наявних інсталяцій нічого не змінюється:
        //     основний канал працює точно як раніше.
        // EN: Additional named Discord channels. The names are set by the user,
        //     not by us: hardcoding "for the schedule"/"for other purposes"
        //     would impose one usage scenario on everyone — for someone else
        //     these will be "test", "English channel" or "moderators". The list
        //     is empty by default, so nothing changes for existing installs:
        //     the primary channel behaves exactly as before.
        public List<DiscordWebhookTarget> DiscordExtraWebhooks { get; set; } = new();
    }

    // UA: Іменований Discord-канал. Окремий клас, а не пара рядків, бо
    //     Microsoft.Extensions.Configuration вміє прив'язувати такі списки
    //     об'єктів з appsettings.json без жодного ручного розбору JSON
    // EN: A named Discord channel. A dedicated class rather than a string pair,
    //     because Microsoft.Extensions.Configuration binds such object lists
    //     from appsettings.json without any manual JSON parsing
    public class DiscordWebhookTarget
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
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