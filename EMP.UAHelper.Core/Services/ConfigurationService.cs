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

        // UA: Чи використовувати Telegram (типово true; відсутнє поле = true)
        // EN: Whether to use Telegram (true by default; a missing field = true)
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
        //     Порожньо або відсутнє поле — резолвиться в Київ
        //     через AppTimeZone.Resolve. При першому запуску вікно FirstRun
        //     автоматично підставляє сюди системну зону
        // EN: Timezone (Windows TZ ID, e.g. "FLE Standard Time") used to
        //     interpret stream date/time across the app. Empty or a
        //     missing field — resolves to Kyiv via AppTimeZone.Resolve. On
        //     first run, the FirstRun window auto-fills this with
        //     the system's own zone
        public string TimeZoneId { get; set; } = string.Empty;

        // UA: Відображувана назва основного Discord-каналу. Порожньо =
        //     показується локалізований підпис за замовчуванням, тож
        //     вигадувати назву не потрібно, якщо канал один
        // EN: Display name of the primary Discord channel. Empty = show the
        //     localized default label, so nobody is forced to invent a name
        //     when they only have one channel
        public string DiscordWebhookName { get; set; } = string.Empty;

        // UA: Додаткові іменовані Discord-канали. Назви задаються в
        //     налаштуваннях довільно (напр. "тест", "англомовний канал",
        //     "модератори") — жодних зашитих сценаріїв використання. Список
        //     порожній за замовчуванням: без додаткових каналів працює лише
        //     основний.
        // EN: Additional named Discord channels. The names are set freely in
        //     settings (e.g. "test", "English channel", "moderators") — no
        //     hardcoded usage scenarios. The list is empty by default:
        //     without additional channels only the primary one is used.
        public List<DiscordWebhookTarget> DiscordExtraWebhooks { get; set; } = new();

        // UA: Збережені іменовані ролі Discord (назва + ID) — для швидкого
        //     вибору в генераторі згадок у вікні ручної відправки, замість
        //     введення "голого" ID наосліп щоразу. Поле DiscordRoleId
        //     (вище) НЕ пов'язане з цим списком — воно відповідає лише за
        //     автоматичний пінг ролі в шаблонних сповіщеннях. Список
        //     порожній за замовчуванням.
        // EN: Saved named Discord roles (name + ID) — for quick selection in
        //     the mention generator in the manual-send window, instead of
        //     blindly typing a "bare" ID every time. The DiscordRoleId
        //     field (above) is NOT tied to this list — it only drives the
        //     automatic role ping in template notifications. The list is
        //     empty by default.
        public List<DiscordRoleTarget> DiscordRoles { get; set; } = new();

        // UA: Тестова відправка. TelegramTestChatId — числовий ID особистого
        //     чату з ботом (бот може писати лише тим, хто натиснув у ньому
        //     /start). DiscordTestWebhookUrl — вебхук окремого тестового
        //     каналу (вебхук Discord не вміє писати в особисті). Порожнє
        //     значення — тестова відправка на цю платформу не виконується
        //     (повернення до основного каналу навмисно немає).
        // EN: Test send. TelegramTestChatId is the numeric ID of the private
        //     chat with the bot (a bot can only message those who pressed
        //     /start in it). DiscordTestWebhookUrl is the webhook of a
        //     separate test channel (a Discord webhook can't send direct
        //     messages). An empty value means no test send to that platform
        //     (falling back to the main channel is deliberately absent).
        public string TelegramTestChatId { get; set; } = string.Empty;

        // UA: true — шаблонні сповіщення в Telegram надсилаються як фото з
        //     мініатюрою YouTube і текстом-підписом замість прев'ю посилання.
        //     false — звичайне прев'ю, а якщо Telegram не зміг його
        //     побудувати (429), автоматично фото з мініатюрою.
        // EN: true — template notifications go to Telegram as a photo with
        //     the YouTube thumbnail and the text as its caption instead of a
        //     link preview. false — the regular preview, and if Telegram
        //     fails to build it (429), a photo with the thumbnail automatically.
        public bool TelegramThumbnailAsPhoto { get; set; }
        public string DiscordTestWebhookUrl { get; set; } = string.Empty;

        // UA: Коли показувати окреме вікно з результатом відправки:
        //     "always" — після кожної відправки, "failures" — лише якщо
        //     хоча б одна платформа не підтвердила успіх, "never" — лише
        //     рядки статусу у вікні відправки. Порожнє, відсутнє або невідоме
        //     значення трактується як "failures".
        // EN: When to show a separate send-result window: "always" — after
        //     every send, "failures" — only if at least one platform didn't
        //     confirm success, "never" — only the status lines in the send
        //     window. An empty, missing or unknown value
        //     is treated as "failures".
        public string SendResultDialog { get; set; } = SendResultDialogMode.Failures;
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

    // UA: Іменована роль Discord (назва + "голий" числовий ID) — той самий
    //     підхід, що й у DiscordWebhookTarget вище, заради однакового
    //     зв'язування Microsoft.Extensions.Configuration
    // EN: A named Discord role (name + "bare" numeric ID) — the same
    //     approach as DiscordWebhookTarget above, for the same
    //     Microsoft.Extensions.Configuration binding
    public class DiscordRoleTarget
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
    }

    // UA: Допустимі значення AppSettings.SendResultDialog
    // EN: The allowed values of AppSettings.SendResultDialog
    public static class SendResultDialogMode
    {
        public const string Always = "always";
        public const string Failures = "failures";
        public const string Never = "never";

        public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            Always => Always,
            Never => Never,
            _ => Failures
        };
    }

    public class ConfigurationService
    {
        // UA: Завантажені налаштування
        // EN: Loaded settings
        public AppSettings Settings { get; private set; }

        public ConfigurationService()
        {
            // UA: appsettings.json шукається поруч з exe
            // EN: appsettings.json is looked up next to the exe
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            Settings = new AppSettings();
            config.Bind(Settings);
        }
    }
}