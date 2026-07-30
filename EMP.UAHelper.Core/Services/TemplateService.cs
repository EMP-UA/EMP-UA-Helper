// =============================================================================
// EMP UA Helper — TemplateService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для завантаження та збереження шаблонів повідомлень
// EN: Service for loading and saving message templates
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Globalization;
using System.Linq;
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

        // UA: Застосувати змінні до шаблону. Рядки, що посилаються на {url} або {twitch},
        //     видаляються цілком, якщо відповідні дані порожні — щоб не лишати биті посилання
        //     для тих, хто не використовує YouTube/Twitch. Часова зона для {scheduled_telegram}
        //     передається явно викликачем (через AppTimeZone.Resolve) — тут нічого не хардкодиться
        // EN: Apply variables to a template. Lines referencing {url} or {twitch}
        //     are removed entirely when the corresponding data is empty — to avoid
        //     dangling links for those who don't use YouTube/Twitch. The timezone for
        //     {scheduled_telegram} is passed explicitly by the caller (via AppTimeZone.Resolve)
        //     — nothing is hardcoded here
        public string Apply(string template, string title, string url,
            string twitchUrl, TimeZoneInfo timeZone, long? scheduledTime = null)
        {
            // UA: Для Discord використовуємо Unix timestamp напряму
            // EN: For Discord we use Unix timestamp directly
            var scheduledDiscord = scheduledTime.HasValue
                ? scheduledTime.Value.ToString()
                : string.Empty;

            // UA: Для Telegram конвертуємо в задану зону з урахуванням літнього/зимнього часу
            // EN: For Telegram convert to the given zone respecting daylight saving time
            var scheduledTelegram = scheduledTime.HasValue
                ? FormatScheduledTelegram(scheduledTime.Value, timeZone)
                : string.Empty;

            // UA: Прибираємо рядки, що посилаються на дані яких немає
            // EN: Strip lines that reference data which isn't available
            var lines = template.Replace("\r\n", "\n").Split('\n');
            var filtered = lines.Where(line =>
                !(string.IsNullOrEmpty(url) && line.Contains(VarUrl)) &&
                !(string.IsNullOrEmpty(twitchUrl) && line.Contains(VarTwitch)));
            var cleaned = string.Join("\n", filtered);

            return cleaned
                .Replace(VarTitle, title)
                .Replace(VarUrl, url)
                .Replace(VarTwitch, twitchUrl)
                .Replace("{scheduled_discord}", scheduledDiscord)
                .Replace("{scheduled_telegram}", scheduledTelegram)
                .Replace(VarScheduled, scheduledDiscord);
        }

        // UA: Форматує Unix-час у читабельний рядок для Telegram у заданій
        //     зоні, з урахуванням її літнього/зимового зсуву (офсет у
        //     дужках обчислюється динамічно, а не хардкодиться). Публічний
        //     і статичний, бо використовується не лише всередині Apply, а й
        //     напряму в UI повністю ручного режиму — там немає шаблону,
        //     який міг би підставити {scheduled_telegram} сам. Саму зону
        //     хардкодити тут не можна — це відповідальність AppTimeZone
        // EN: Formats a Unix timestamp into a human-readable Telegram string
        //     in the given zone, respecting its own daylight saving shift
        //     (the offset in parentheses is computed dynamically, not
        //     hardcoded). Public and static because it's used not only
        //     inside Apply but also directly by the fully manual mode UI —
        //     there's no template there to substitute {scheduled_telegram}
        //     automatically. The zone itself must not be hardcoded here —
        //     that's AppTimeZone's responsibility
        public static string FormatScheduledTelegram(long unixSeconds, TimeZoneInfo timeZone)
        {
            var localTime = TimeZoneInfo.ConvertTime(
                DateTimeOffset.FromUnixTimeSeconds(unixSeconds),
                timeZone);

            // UA: DateTimeOffset.Offset після ConvertTime уже враховує
            //     літній/зимовий час цільової зони на цей конкретний момент —
            //     тут нічого додатково визначати не треба
            // EN: DateTimeOffset.Offset after ConvertTime already accounts
            //     for the target zone's daylight saving at that specific
            //     moment — nothing extra to determine here
            var offset = localTime.Offset;
            var offsetLabel = offset < TimeSpan.Zero
                ? $"UTC-{-offset:hh\\:mm}"
                : $"UTC+{offset:hh\\:mm}";

            // UA: Формуємо дату окремо від офсету й склеюємо рядки напряму —
            //     якщо передати offsetLabel (із двокрапкою всередині) як
            //     частину рядка формату для ToString, ":" там сприймається
            //     не як текст, а як роздільник часу з поточної культури, що
            //     теоретично могло б дати інший символ у нетипових культурах
            // EN: Build the date and the offset separately and concatenate
            //     plain strings — passing offsetLabel (which contains a
            //     colon) as part of the ToString format string would make
            //     .NET treat ":" as the culture's time separator token
            //     rather than literal text, which could in theory render
            //     differently under an unusual culture
            var datePart = localTime.ToString("d MMMM о HH:mm",
                new CultureInfo("uk-UA"));
            return $"{datePart} ({offsetLabel})";
        }

        // UA: Готовий до вставки Discord timestamp-тег — той самий формат,
        //     що й у шаблоні Upcoming (<t:...:F> — повна дата, <t:...:R> —
        //     "через N годин"), Discord рендерить обидва варіанти в
        //     локальному часі читача автоматично, без нашого коду
        // EN: A ready-to-paste Discord timestamp tag — the same format used
        //     in the Upcoming template (<t:...:F> — full date, <t:...:R> —
        //     "in N hours"), Discord renders both in the reader's local time
        //     automatically, no code on our side needed
        public static string FormatScheduledDiscordSnippet(long unixSeconds) =>
            $"<t:{unixSeconds}:F> (<t:{unixSeconds}:R>)";

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