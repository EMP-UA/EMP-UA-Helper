// =============================================================================
// EMP UA Helper — AppTimeZone.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Єдине місце визначення часової зони для дат трансляцій. Раніше
//     "FLE Standard Time" (Київ) було захардкоджено в трьох різних місцях
//     коду (TemplateService, двічі в SendNotificationWindow) — тепер уся
//     логіка резолву в одному класі.
//
//     Правила:
//     1. Якщо в AppSettings.TimeZoneId явно вказана коректна зона —
//        використовуємо саме її.
//     2. Якщо поле порожнє (типово — старий appsettings.json, збережений
//        до появи цього налаштування) — падаємо назад на Київ, щоб
//        поведінка для вже наявних інсталяцій не змінилась.
//     3. Системна зона (TimeZoneInfo.Local) ніколи не читається "наживо"
//        під час роботи програми — лише один раз, як початкове значення
//        для попереднього заповнення поля при першому запуску. Це не
//        "здогадка" в сенсі невизначеності — дані точні (це реальна
//        поточна зона ОС), просто вони використовуються лише як стартове
//        значення, яке користувач може одразу змінити, а не як живе
//        джерело правди. Свідомо: якщо комп'ютер стримера тимчасово
//        перелаштується на іншу зону (відрядження, VPN, помилка Windows),
//        уже збережені сповіщення не повинні "поплисти" самі собою.
//
// EN: Single place that resolves the timezone for stream dates. Previously
//     "FLE Standard Time" (Kyiv) was hardcoded in three different spots
//     (TemplateService, twice in SendNotificationWindow) — now all the
//     resolution logic lives in one class.
//
//     Rules:
//     1. If AppSettings.TimeZoneId explicitly names a valid zone — use it.
//     2. If the field is empty (typically an old appsettings.json saved
//        before this setting existed) — fall back to Kyiv, so behavior
//        for existing installs doesn't change.
//     3. The system zone (TimeZoneInfo.Local) is never read "live" while
//        the app is running — only once, as the initial value used to
//        pre-fill the field on first run. This isn't a "guess" in the
//        sense of being uncertain — the data is exact (it's the OS's
//        real current zone), it's just used only as a starting value the
//        user can immediately change, not as a live source of truth.
//        Intentional: if the streamer's machine temporarily reports a
//        different zone (travel, VPN, a Windows misconfiguration),
//        already-scheduled announcements shouldn't silently drift.
// =============================================================================
using System.Collections.Generic;
using System.Linq;

namespace EMP.UAHelper.Core.Services
{
    public static class AppTimeZone
    {
        // UA: Резервна зона для порожнього/некоректного TimeZoneId —
        //     Windows ID для Східної Європи (охоплює Київ, коректно
        //     обробляє перехід літній/зимовий час)
        // EN: Fallback zone for an empty/invalid TimeZoneId — the Windows
        //     ID for Eastern Europe (covers Kyiv, correctly handles the
        //     daylight saving transition)
        public const string FallbackId = "FLE Standard Time";

        // UA: Визначити TimeZoneInfo за налаштованим ID. Некоректний або
        //     видалений (наприклад, оновленням Windows) ID тихо falls back
        //     на Київ — щоб пошкоджений appsettings.json не валив програму
        // EN: Resolve a TimeZoneInfo from the configured ID. An invalid or
        //     removed (e.g. by a Windows update) ID silently falls back to
        //     Kyiv — so a corrupted appsettings.json doesn't crash the app
        public static TimeZoneInfo Resolve(string? configuredId)
        {
            if (!string.IsNullOrWhiteSpace(configuredId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(configuredId);
                }
                catch (TimeZoneNotFoundException) { /* UA: падаємо на фолбек нижче / EN: fall through to the fallback below */ }
                catch (InvalidTimeZoneException) { /* UA: те саме / EN: same */ }
            }

            return TimeZoneInfo.FindSystemTimeZoneById(FallbackId);
        }

        // UA: Поточна системна зона ОС — читається точно, без невизначеності,
        //     але використовується виключно як початкове значення для
        //     попереднього заповнення поля у вікні першого запуску, а не як
        //     джерело правди під час роботи програми (див. правило 3 вище)
        // EN: The OS's current system zone — read exactly, with no
        //     uncertainty, but used solely as the initial value to pre-fill
        //     the field in the first-run window, not as a runtime source of
        //     truth (see rule 3 above)
        public static string DetectSystemId()
        {
            try
            {
                return TimeZoneInfo.Local.Id;
            }
            catch
            {
                return FallbackId;
            }
        }

        // UA: Усі доступні на машині часові зони, відсортовані за зсувом
        //     UTC — для комбобокса вибору в UI. Мова назв (TimeZoneInfo.
        //     DisplayName) визначається мовою самої Windows, а не нашим
        //     LocalizationService/_loc.Language — це системні рядки ОС,
        //     не наші, тому перемикач UA/EN у програмі на них не впливає
        // EN: All timezones available on the machine, sorted by UTC offset
        //     — for the selection combo box in the UI. The language of the
        //     names (TimeZoneInfo.DisplayName) is determined by Windows'
        //     own language, not by our LocalizationService/_loc.Language —
        //     these are OS-provided strings, not ours, so the app's UA/EN
        //     switch has no effect on them
        public static List<TimeZoneInfo> AllZones() =>
            TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(z => z.BaseUtcOffset)
                .ThenBy(z => z.DisplayName)
                .ToList();
    }
}
