// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс локалізації інтерфейсу (UA/EN)
// EN: UI localization service (UA/EN)
namespace EMP.UAHelper.Core.Services
{
    public enum UiLanguage { UA, EN }

    public class LocalizationService
    {
        public UiLanguage Language { get; private set; } = UiLanguage.UA;

        public event Action? LanguageChanged;

        public void SetLanguage(UiLanguage language)
        {
            Language = language;
            LanguageChanged?.Invoke();
        }

        public string Get(string key) =>
            Language == UiLanguage.UA
                ? UA.TryGetValue(key, out var ua) ? ua : key
                : EN.TryGetValue(key, out var en) ? en : key;

        private static readonly Dictionary<string, string> UA = new()
        {
            ["app.name"] = "EMP UA Helper",
            ["app.save"] = "💾 Зберегти",
            ["app.cancel"] = "Скасувати",
            ["app.exit"] = "Вихід",
            ["app.success"] = "Успіх",
            ["app.error"] = "Помилка",
            ["app.warning"] = "Увага",
            ["app.yes"] = "Так",
            ["app.no"] = "Ні",

            ["tray.send"] = "📡 Надіслати сповіщення",
            ["tray.manual"] = "✍️ Ручне сповіщення",
            ["tray.edit_templates"] = "✏️ Редагувати шаблони",
            ["tray.settings"] = "⚙️ Налаштування",
            ["tray.exit"] = "Вихід",
            ["tray.checking"] = "EMP UA Helper — Перевірка...",
            ["tray.no_result"] = "YouTube API не повернув жодного результату.",
            ["tray.sending"] = "Відправка сповіщень у Telegram та Discord...",
            ["tray.sent"] = "Сповіщення надіслано.",
            ["tray.started"] = "EMP UA Helper запущено.",
            ["tray.stopped"] = "EMP UA Helper завершено.",

            ["type.live"] = "🟢 Трансляція",
            ["type.upcoming"] = "🔔 Анонс",
            ["type.video"] = "💾 Відео",
            ["type.short"] = "⚡ Шортс",

            ["firstrun.title"] = "EMP UA Helper — Перший запуск",
            ["firstrun.welcome"] = "Ласкаво просимо до EMP UA Helper",
            ["firstrun.description"] = "Заповніть налаштування для потрібних вам платформ. Це можна будь-коли змінити пізніше через трей → «⚙️ Налаштування» — без перезапуску програми.",
            ["firstrun.save"] = "💾 Зберегти та запустити",
            ["firstrun.validation"] = "Будь ласка, заповніть усі поля увімкнених платформ.",
            ["firstrun.validation.platform"] = "Оберіть принаймні одну платформу сповіщень: Telegram або Discord.",
            ["firstrun.section.telegram"] = "TELEGRAM",
            ["firstrun.section.youtube"] = "YOUTUBE",
            ["firstrun.section.discord"] = "DISCORD",
            ["firstrun.section.twitch"] = "TWITCH",
            ["firstrun.use.telegram"] = "Використовувати Telegram",
            ["firstrun.use.youtube"] = "Використовувати YouTube",
            ["firstrun.use.discord"] = "Використовувати Discord",
            ["firstrun.use.twitch"] = "Додавати посилання на Twitch",
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

            ["settings.title"] = "EMP UA Helper — Налаштування",
            ["settings.header"] = "Налаштування платформ",
            ["settings.description"] = "Оберіть, які платформи активні саме сьогодні. Зміни застосовуються одразу, без перезапуску програми.",
            ["settings.save"] = "💾 Зберегти зміни",
            ["settings.group.content"] = "ДЖЕРЕЛА КОНТЕНТУ (де ви стрімите / публікуєте)",
            ["settings.group.notify"] = "ПЛАТФОРМИ СПОВІЩЕНЬ (куди надсилати анонси)",

            ["templates.title"] = "EMP UA Helper — Редактор шаблонів",
            ["templates.header"] = "Редактор шаблонів повідомлень",
            ["templates.hint"] = "Змінні: {title} — назва    {url} — YouTube    {twitch} — Twitch    {scheduled_telegram} — дата/час (Telegram, Upcoming)    {scheduled_discord} — Unix час (Discord, Upcoming)",
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

            ["manual.title"] = "EMP UA Helper — Ручне сповіщення",
            ["manual.header"] = "Ручне сповіщення",
            ["manual.description"] = "Сповіщення незалежне від автоматичного виявлення YouTube — використовуйте його для будь-якого анонсу, коли він потрібен саме зараз.",
            ["manual.title_label"] = "Заголовок",
            ["manual.type_label"] = "Тип",
            ["manual.type_hint"] = "Категорія впливає лише на шаблон і колір сповіщення — не залежить від конкретної платформи.",
            ["manual.url_label"] = "Посилання (необов'язково)",
            ["manual.url_hint"] = "Якщо вказати посилання — воно підставиться замість {url} у шаблоні. Якщо залишити порожнім — рядки шаблону з {url} просто не увійдуть у повідомлення.",
            ["manual.date_label"] = "Дата початку (дд.MM.рррр)",
            ["manual.time_label"] = "Час початку (гг:хх, Київ)",
            ["manual.send"] = "📡 Надіслати",
            ["manual.validation.title"] = "Введіть заголовок.",
            ["manual.validation.datetime"] = "Введіть коректні дату і час у форматі дд.MM.рррр та гг:хх.",
            ["manual.sent"] = "Сповіщення надіслано.",
        };

        private static readonly Dictionary<string, string> EN = new()
        {
            ["app.name"] = "EMP UA Helper",
            ["app.save"] = "💾 Save",
            ["app.cancel"] = "Cancel",
            ["app.exit"] = "Exit",
            ["app.success"] = "Success",
            ["app.error"] = "Error",
            ["app.warning"] = "Warning",
            ["app.yes"] = "Yes",
            ["app.no"] = "No",

            ["tray.send"] = "📡 Send notification",
            ["tray.manual"] = "✍️ Manual notification",
            ["tray.edit_templates"] = "✏️ Edit templates",
            ["tray.settings"] = "⚙️ Settings",
            ["tray.exit"] = "Exit",
            ["tray.checking"] = "EMP UA Helper — Checking...",
            ["tray.no_result"] = "YouTube API returned no results.",
            ["tray.sending"] = "Sending notifications to Telegram and Discord...",
            ["tray.sent"] = "Notifications sent.",
            ["tray.started"] = "EMP UA Helper started.",
            ["tray.stopped"] = "EMP UA Helper stopped.",

            ["type.live"] = "🟢 Live stream",
            ["type.upcoming"] = "🔔 Upcoming stream",
            ["type.video"] = "💾 Video",
            ["type.short"] = "⚡ Short",

            ["firstrun.title"] = "EMP UA Helper — First Run",
            ["firstrun.welcome"] = "Welcome to EMP UA Helper",
            ["firstrun.description"] = "Fill in the settings for the platforms you need. You can change this anytime later via tray → \u201c⚙️ Settings\u201d — without restarting the app.",
            ["firstrun.save"] = "💾 Save and Launch",
            ["firstrun.validation"] = "Please fill in all fields for the enabled platforms.",
            ["firstrun.validation.platform"] = "Please select at least one notification platform: Telegram or Discord.",
            ["firstrun.section.telegram"] = "TELEGRAM",
            ["firstrun.section.youtube"] = "YOUTUBE",
            ["firstrun.section.discord"] = "DISCORD",
            ["firstrun.section.twitch"] = "TWITCH",
            ["firstrun.use.telegram"] = "Use Telegram",
            ["firstrun.use.youtube"] = "Use YouTube",
            ["firstrun.use.discord"] = "Use Discord",
            ["firstrun.use.twitch"] = "Include Twitch link",
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

            ["settings.title"] = "EMP UA Helper — Settings",
            ["settings.header"] = "Platform Settings",
            ["settings.description"] = "Choose which platforms are active today. Changes apply immediately, without restarting the app.",
            ["settings.save"] = "💾 Save changes",
            ["settings.group.content"] = "CONTENT SOURCES (where you stream / publish)",
            ["settings.group.notify"] = "NOTIFICATION PLATFORMS (where to send announcements)",

            ["templates.title"] = "EMP UA Helper — Template Editor",
            ["templates.header"] = "Message Template Editor",
            ["templates.hint"] = "Variables: {title} — title    {url} — YouTube    {twitch} — Twitch    {scheduled_telegram} — date/time (Telegram, Upcoming)    {scheduled_discord} — Unix time (Discord, Upcoming)",
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

            ["manual.title"] = "EMP UA Helper — Manual Notification",
            ["manual.header"] = "Manual Notification",
            ["manual.description"] = "A notification independent of YouTube auto-detection — use it for any announcement whenever you need one right now.",
            ["manual.title_label"] = "Title",
            ["manual.type_label"] = "Type",
            ["manual.type_hint"] = "The category only affects the message template and color — it's not tied to any specific platform.",
            ["manual.url_label"] = "Link (optional)",
            ["manual.url_hint"] = "If you provide a link, it replaces {url} in the template. If left empty, template lines containing {url} are simply omitted from the message.",
            ["manual.date_label"] = "Start date (dd.MM.yyyy)",
            ["manual.time_label"] = "Start time (HH:mm, Kyiv)",
            ["manual.send"] = "📡 Send",
            ["manual.validation.title"] = "Please enter a title.",
            ["manual.validation.datetime"] = "Enter a valid date and time in dd.MM.yyyy and HH:mm format.",
            ["manual.sent"] = "Notification sent.",
        };
    }
}