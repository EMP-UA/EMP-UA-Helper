// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс локалізації інтерфейсу (UA/EN)
// EN: UI localization service (UA/EN)
namespace EMP.UAHelper.Core.Services
{
    public enum UiLanguage { UA, EN }

    public class LocalizationService
    {
        // UA: Поточна мова інтерфейсу
        // EN: Current UI language
        public UiLanguage Language { get; private set; } = UiLanguage.UA;

        // UA: Подія зміни мови — підписуються всі вікна
        // EN: Language changed event — all windows subscribe
        public event Action? LanguageChanged;

        public void SetLanguage(UiLanguage language)
        {
            Language = language;
            LanguageChanged?.Invoke();
        }

        // UA: Отримати рядок за ключем
        // EN: Get string by key
        public string Get(string key) =>
            Language == UiLanguage.UA
                ? UA.TryGetValue(key, out var ua) ? ua : key
                : EN.TryGetValue(key, out var en) ? en : key;

        // UA: Рядки українською
        // EN: Ukrainian strings
        private static readonly Dictionary<string, string> UA = new()
        {
            // UA: Загальне / EN: General
            ["app.name"] = "EMP UA Helper",
            ["app.save"] = "💾 Зберегти",
            ["app.cancel"] = "Скасувати",
            ["app.exit"] = "Вихід",
            ["app.success"] = "Успіх",
            ["app.error"] = "Помилка",
            ["app.warning"] = "Увага",
            ["app.yes"] = "Так",
            ["app.no"] = "Ні",

            // UA: Трей / EN: Tray
            ["tray.send"] = "📡 Надіслати сповіщення",
            ["tray.edit_templates"] = "✏️ Редагувати шаблони",
            ["tray.settings"] = "⚙️ Налаштування",
            ["tray.exit"] = "Вихід",
            ["tray.checking"] = "EMP UA Helper — Перевірка...",
            ["tray.no_result"] = "YouTube API не повернув жодного результату.",
            ["tray.sending"] = "Відправка сповіщень у Telegram та Discord...",
            ["tray.sent"] = "Сповіщення надіслано.",
            ["tray.started"] = "EMP UA Helper запущено.",
            ["tray.stopped"] = "EMP UA Helper завершено.",

            // UA: Типи контенту / EN: Content types
            ["type.live"] = "🟢 Трансляція",
            ["type.upcoming"] = "🔔 Анонс",
            ["type.video"] = "💾 Відео",
            ["type.short"] = "⚡ Шортс",

            // UA: Вікно першого запуску / EN: First run window
            ["firstrun.title"] = "EMP UA Helper — Перший запуск",
            ["firstrun.welcome"] = "Ласкаво просимо до EMP UA Helper",
            ["firstrun.description"] = "Заповніть налаштування для підключення до Telegram, YouTube та Discord. Дані зберігаються локально поруч з програмою.",
            ["firstrun.save"] = "💾 Зберегти та запустити",
            ["firstrun.validation"] = "Будь ласка, заповніть усі поля.",
            ["firstrun.section.telegram"] = "TELEGRAM",
            ["firstrun.section.youtube"] = "YOUTUBE",
            ["firstrun.section.discord"] = "DISCORD",
            ["firstrun.section.twitch"] = "TWITCH",
            ["firstrun.tg.token"] = "Bot Token",
            ["firstrun.tg.token.hint"] = "@BotFather → /newbot або /token",
            ["firstrun.tg.channel"] = "Канал-отримувач",
            ["firstrun.tg.channel.hint"] = "Username каналу куди бот надсилає повідомлення",
            ["firstrun.yt.key"] = "API Key",
            ["firstrun.yt.key.hint"] = "console.cloud.google.com → Credentials → API Key → YouTube Data API v3",
            ["firstrun.yt.channelid"] = "Channel ID",
            ["firstrun.yt.channelid.hint"] = "youtube.com/@канал → Персоналізувати канал → URL-адреса каналу",
            ["firstrun.dc.webhook"] = "Webhook URL",
            ["firstrun.dc.webhook.hint"] = "Редагувати канал → Інтеграції → Вебхуки → Новий вебхук → Скопіювати URL",
            ["firstrun.dc.roleid"] = "Role ID (для пінгу)",
            ["firstrun.dc.roleid.hint"] = "Налаштування сервера → Ролі → ПКМ на роль → Копіювати ID ролі (потрібен режим розробника)",
            ["firstrun.tw.url"] = "Channel URL",
            ["firstrun.tw.url.hint"] = "https://www.twitch.tv/your_channel",

            // UA: Редактор шаблонів / EN: Template editor
            ["templates.title"] = "EMP UA Helper — Редактор шаблонів",
            ["templates.header"] = "Редактор шаблонів повідомлень",
            ["templates.hint"] = "Доступні змінні: {title} — назва відео    {url} — посилання YouTube    {twitch} — посилання Twitch",
            ["templates.tab.telegram"] = "Telegram",
            ["templates.tab.discord"] = "Discord",
            ["templates.discord.title_label"] = "Заголовок",
            ["templates.discord.body_label"] = "Тіло",
            ["templates.reset"] = "↺ Скинути до defaults",
            ["templates.save"] = "💾 Зберегти",
            ["templates.saved"] = "Шаблони збережено успішно!",
            ["templates.reset.confirm"] = "Скинути всі шаблони до стандартних значень?",
            ["templates.reset.done"] = "Шаблони скинуто до стандартних значень.",
            ["templates.live"] = "🟢 Live — активна трансляція",
            ["templates.upcoming"] = "🔔 Upcoming — запланована трансляція",
            ["templates.video"] = "💾 Video — звичайне відео",
            ["templates.short"] = "⚡ Short — шортс",
        };

        // UA: Рядки англійською
        // EN: English strings
        private static readonly Dictionary<string, string> EN = new()
        {
            // General
            ["app.name"] = "EMP UA Helper",
            ["app.save"] = "💾 Save",
            ["app.cancel"] = "Cancel",
            ["app.exit"] = "Exit",
            ["app.success"] = "Success",
            ["app.error"] = "Error",
            ["app.warning"] = "Warning",
            ["app.yes"] = "Yes",
            ["app.no"] = "No",

            // Tray
            ["tray.send"] = "📡 Send notification",
            ["tray.edit_templates"] = "✏️ Edit templates",
            ["tray.settings"] = "⚙️ Settings",
            ["tray.exit"] = "Exit",
            ["tray.checking"] = "EMP UA Helper — Checking...",
            ["tray.no_result"] = "YouTube API returned no results.",
            ["tray.sending"] = "Sending notifications to Telegram and Discord...",
            ["tray.sent"] = "Notifications sent.",
            ["tray.started"] = "EMP UA Helper started.",
            ["tray.stopped"] = "EMP UA Helper stopped.",

            // Content types
            ["type.live"] = "🟢 Live stream",
            ["type.upcoming"] = "🔔 Upcoming stream",
            ["type.video"] = "💾 Video",
            ["type.short"] = "⚡ Short",

            // First run window
            ["firstrun.title"] = "EMP UA Helper — First Run",
            ["firstrun.welcome"] = "Welcome to EMP UA Helper",
            ["firstrun.description"] = "Fill in the settings to connect Telegram, YouTube and Discord. Data is stored locally next to the application.",
            ["firstrun.save"] = "💾 Save and Launch",
            ["firstrun.validation"] = "Please fill in all fields.",
            ["firstrun.section.telegram"] = "TELEGRAM",
            ["firstrun.section.youtube"] = "YOUTUBE",
            ["firstrun.section.discord"] = "DISCORD",
            ["firstrun.section.twitch"] = "TWITCH",
            ["firstrun.tg.token"] = "Bot Token",
            ["firstrun.tg.token.hint"] = "@BotFather → /newbot or /token",
            ["firstrun.tg.channel"] = "Target Channel",
            ["firstrun.tg.channel.hint"] = "Channel username where the bot sends messages",
            ["firstrun.yt.key"] = "API Key",
            ["firstrun.yt.key.hint"] = "console.cloud.google.com → Credentials → API Key → YouTube Data API v3",
            ["firstrun.yt.channelid"] = "Channel ID",
            ["firstrun.yt.channelid.hint"] = "youtube.com/@channel → Customize channel → Channel URL",
            ["firstrun.dc.webhook"] = "Webhook URL",
            ["firstrun.dc.webhook.hint"] = "Edit Channel → Integrations → Webhooks → New Webhook → Copy URL",
            ["firstrun.dc.roleid"] = "Role ID (for mention)",
            ["firstrun.dc.roleid.hint"] = "Server Settings → Roles → Right-click role → Copy Role ID (requires Developer Mode)",
            ["firstrun.tw.url"] = "Channel URL",
            ["firstrun.tw.url.hint"] = "https://www.twitch.tv/your_channel",

            // Template editor
            ["templates.title"] = "EMP UA Helper — Template Editor",
            ["templates.header"] = "Message Template Editor",
            ["templates.hint"] = "Available variables: {title} — video title    {url} — YouTube link    {twitch} — Twitch link",
            ["templates.tab.telegram"] = "Telegram",
            ["templates.tab.discord"] = "Discord",
            ["templates.discord.title_label"] = "Title",
            ["templates.discord.body_label"] = "Body",
            ["templates.reset"] = "↺ Reset to defaults",
            ["templates.save"] = "💾 Save",
            ["templates.saved"] = "Templates saved successfully!",
            ["templates.reset.confirm"] = "Reset all templates to default values?",
            ["templates.reset.done"] = "Templates reset to default values.",
            ["templates.live"] = "🟢 Live — active stream",
            ["templates.upcoming"] = "🔔 Upcoming — scheduled stream",
            ["templates.video"] = "💾 Video — regular video",
            ["templates.short"] = "⚡ Short — short video",
        };
    }
}