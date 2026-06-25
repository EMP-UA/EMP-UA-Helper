// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для завантаження та збереження шаблонів повідомлень
// EN: Service for loading and saving message templates
using EMP.UAHelper.Core.Models;
using System.Globalization;
using System.Text.Json;

namespace EMP.UAHelper.Core.Services
{
    public class TemplateService
    {
        private readonly string _templatesPath;
        private MessageTemplates _templates;

        // UA: Змінні які можна використовувати в шаблонах
        // EN: Variables available in templates
        public const string VarTitle = "{title}";
        public const string VarUrl = "{url}";
        public const string VarTwitch = "{twitch}";
        public const string VarScheduled = "{scheduled}";

        public TemplateService()
        {
            _templatesPath = Path.Combine(AppContext.BaseDirectory, "templates.json");
            _templates = LoadOrCreateDefault();
        }

        // UA: Отримати поточні шаблони
        // EN: Get current templates
        public MessageTemplates GetTemplates() => _templates;

        // UA: Зберегти оновлені шаблони
        // EN: Save updated templates
        public async Task SaveAsync(MessageTemplates templates)
        {
            _templates = templates;
            var json = JsonSerializer.Serialize(templates,
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_templatesPath, json);
        }

        // UA: Застосувати змінні до шаблону
        // EN: Apply variables to template
        public string Apply(string template, string title, string url,
            string twitchUrl, long? scheduledTime = null)
        {
            // UA: Для Discord використовуємо Unix timestamp напряму
            // EN: For Discord we use Unix timestamp directly
            var scheduledDiscord = scheduledTime.HasValue
                ? scheduledTime.Value.ToString()
                : string.Empty;

            // UA: Для Telegram конвертуємо в київський час з урахуванням літнього/зимнього часу
            // EN: For Telegram convert to Kyiv time respecting daylight saving time
            var scheduledTelegram = string.Empty;
            if (scheduledTime.HasValue)
            {
                var kyivZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
                var kyivTime = TimeZoneInfo.ConvertTime(
                    DateTimeOffset.FromUnixTimeSeconds(scheduledTime.Value),
                    kyivZone);
                var offset = kyivZone.IsDaylightSavingTime(kyivTime.DateTime)
                    ? "UTC+3"
                    : "UTC+2";
                scheduledTelegram = kyivTime
                    .ToString($"d MMMM о HH:mm ({offset})",
                        new CultureInfo("uk-UA"));
            }

            return template
                .Replace(VarTitle, title)
                .Replace(VarUrl, url)
                .Replace(VarTwitch, twitchUrl)
                .Replace("{scheduled_discord}", scheduledDiscord)
                .Replace("{scheduled_telegram}", scheduledTelegram)
                .Replace(VarScheduled, scheduledDiscord);
        }

        // UA: Завантажити шаблони з файлу або створити дефолтні
        // EN: Load templates from file or create defaults
        private MessageTemplates LoadOrCreateDefault()
        {
            if (File.Exists(_templatesPath))
            {
                var json = File.ReadAllText(_templatesPath);
                return JsonSerializer.Deserialize<MessageTemplates>(json) ?? CreateDefault();
            }

            var defaults = CreateDefault();
            File.WriteAllText(_templatesPath,
                JsonSerializer.Serialize(defaults,
                    new JsonSerializerOptions { WriteIndented = true }));
            return defaults;
        }

        // UA: Дефолтні шаблони в тематиці EMP
        // EN: Default templates in EMP theme
        private MessageTemplates CreateDefault() => new()
        {
            Telegram = new PlatformTemplates
            {
                Live =
                    "⚡ <b>Сигнал встановлюється...</b>\n" +
                    "📥 <b>Розшифровка:</b> {title}\n\n" +
                    "🔴 <a href=\"{url}\">YouTube</a>\n" +
                    "🟣 <a href=\"{twitch}\">Twitch</a>\n\n" +
                    "#EMP_трансляції",
                Upcoming =
                    "🔔 <b>Імпульс готується до передачі. Очікуйте сигналу.</b>\n" +
                    "📥 <b>Ціль:</b> {title}\n" +
                    "🗓 <b>Початок:</b> {scheduled_telegram}\n\n" +
                    "🔴 <a href=\"{url}\">YouTube</a>\n" +
                    "🟣 <a href=\"{twitch}\">Twitch</a>\n\n" +
                    "#EMP_трансляції",
                Video =
                    "💾 <b>Новий пакет даних на каналі. Декодування завершено.</b>\n" +
                    "📥 <b>Файл:</b> {title}\n\n" +
                    "🔴 <a href=\"{url}\">Завантажити візуалізацію</a>",
                Short =
                    "⚡ <b>Короткий імпульс у ефірі.</b>\n" +
                    "📱 {title}\n\n" +
                    "🔴 <a href=\"{url}\">Прийняти сигнал</a>"
            },
            Discord = new PlatformTemplates
            {
                Live =
                    "📥 **Розшифровка:** {title}\n\n" +
                    "🔴 [YouTube]({url})\n🟣 [Twitch]({twitch})",
                Upcoming =
                    "📥 **Ціль:** {title}\n" +
                    "🗓️ **Початок:** <t:{scheduled_discord}:F> (<t:{scheduled_discord}:R>)\n\n" +
                    "🔴 [YouTube]({url})\n🟣 [Twitch]({twitch})",
                Video =
                    "📥 **Файл:** {title}\n\n" +
                    "🔴 [Завантажити візуалізацію]({url})",
                Short =
                    "📱 {title}\n\n" +
                    "🔴 [Прийняти сигнал]({url})"
            },
            DiscordTitles = new PlatformTemplates
            {
                Live = "🟢 Зафіксовано EMP-імпульс. Радіотишу порушено.",
                Upcoming = "🔔 Імпульс готується до передачі. Очікуйте сигналу.",
                Video = "💾 На радарі зафіксовано новий пакет даних. Декодування завершено.",
                Short = "⚡ Короткий імпульс у ефірі."
            }
        };
    }
}