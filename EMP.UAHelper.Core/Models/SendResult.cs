// =============================================================================
// EMP UA Helper — SendResult.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Структурований результат відправки сповіщення. Раніше
//     NotificationDispatcher або мовчки "все вдалось" (await без перевірки),
//     або кидав виняток, що ховав інформацію про ІНШУ платформу — якщо
//     Telegram впав, ми не дізнавались, чи Discord встиг відправитись
//     (і навпаки). Ці типи дають по одному явному результату на кожну
//     платформу незалежно від того, що сталось з іншою.
// EN: A structured notification-send result. Previously NotificationDispatcher
//     either silently assumed success (an un-checked await) or threw, which hid
//     information about the OTHER platform — if Telegram failed, we never found
//     out whether Discord had already gone through (and vice versa). These
//     types give one explicit result per platform, independent of what
//     happened to the other.
// =============================================================================
namespace EMP.UAHelper.Core.Models
{
    // UA: Платформа, якої стосується результат
    // EN: The platform a result refers to
    public enum NotificationPlatform
    {
        Telegram,
        Discord
    }

    // UA: Результат відправки на ОДНУ платформу.
    //     Attempted=false означає "платформа не налаштована або для неї не
    //     було тексту" — це нормальний, очікуваний стан, а не помилка (напр.
    //     ручне сповіщення лише в Discord — Telegram тут Attempted=false).
    //     ConfirmationId — ID повідомлення від самої платформи (Discord
    //     message id чи Telegram MessageId), доступний лише якщо Succeeded.
    //     Може бути null навіть при Succeeded=true, якщо платформа не
    //     повернула ID (малоймовірно, але код не повинен на це покладатись).
    // EN: The result of sending to ONE platform.
    //     Attempted=false means "the platform isn't configured, or had no
    //     text for it" — a normal, expected state, not an error (e.g. a
    //     manual notification sent to Discord only — Telegram is
    //     Attempted=false here).
    //     ConfirmationId — the message ID returned by the platform itself
    //     (a Discord message id or a Telegram MessageId), available only when
    //     Succeeded. May be null even when Succeeded=true if the platform
    //     didn't return an ID (unlikely, but code shouldn't rely on it).
    public class PlatformSendResult
    {
        public required NotificationPlatform Platform { get; init; }
        public required bool Attempted { get; init; }
        public required bool Succeeded { get; init; }
        public string? ConfirmationId { get; init; }
        public Exception? Error { get; init; }

        // UA: Короткий, зручний для показу в UI/логах підсумок одного рядка
        // EN: A short, UI/log-friendly one-line summary
        public string Summary()
        {
            if (!Attempted)
                return $"{Platform}: —";

            return Succeeded
                ? $"{Platform}: ✓" + (ConfirmationId is null ? string.Empty : $" (ID {ConfirmationId})")
                : $"{Platform}: ✗ {Error?.Message ?? "невідома помилка / unknown error"}";
        }
    }

    // UA: Сукупний результат однієї спроби відправки на обидві платформи
    // EN: The combined result of one send attempt across both platforms
    public class SendResult
    {
        public required PlatformSendResult Telegram { get; init; }
        public required PlatformSendResult Discord { get; init; }

        // UA: Чи хоч одна платформа, яку намагались використати, впала
        // EN: Whether any attempted platform failed
        public bool AnyFailed => (Telegram.Attempted && !Telegram.Succeeded) || (Discord.Attempted && !Discord.Succeeded);

        // UA: Чи хоч одна відправка реально вдалась
        // EN: Whether any send actually succeeded
        public bool AnySucceeded => Telegram.Succeeded || Discord.Succeeded;

        // UA: Чи все, що намагались відправити, вдалось (використовується,
        //     щоб вирішити, чи можна безпечно закрити вікно відправки)
        // EN: Whether everything attempted succeeded (used to decide whether
        //     the send window can be safely closed)
        public bool AllAttemptsSucceeded =>
            (!Telegram.Attempted || Telegram.Succeeded) && (!Discord.Attempted || Discord.Succeeded);
    }
}
