// =============================================================================
// EMP UA Helper — NotificationDispatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Єдина точка відправки сповіщень — не залежить від джерела VideoInfo
//     (YouTube-автовиявлення чи ручний ввід дають той самий результат)
// EN: Single dispatch point for notifications — independent of the VideoInfo
//     source (YouTube auto-detection or manual entry produce the same result)
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;

namespace EMP.UAHelper.Core.Services
{
    public class NotificationDispatcher
    {
        private readonly TelegramService? _telegramService;
        private readonly DiscordService? _discordService;
        private readonly CrashLogService _crashLogService;

        public NotificationDispatcher(
            TelegramService? telegramService,
            DiscordService? discordService,
            CrashLogService crashLogService)
        {
            _telegramService = telegramService;
            _discordService = discordService;
            _crashLogService = crashLogService;
        }

        // UA: Виконує відправку на одну платформу й ЗАВЖДИ повертає явний
        //     результат — ніколи не кидає далі. Це свідомо замінює старий
        //     підхід з Task.WhenAll(tasks), де виняток з однієї платформи
        //     (напр. Telegram 429) обривав виклик і ховав те, що друга
        //     платформа (Discord) могла вже реально відправити повідомлення.
        //     Кожен результат одразу й повністю логується через
        //     CrashLogService — досі логувався лише НАМІР відправити, а не
        //     фактичний результат, тому в логах неможливо було побачити, що
        //     Discord отримав повідомлення, а Telegram — ні (чи навпаки).
        // EN: Runs a send attempt for one platform and ALWAYS returns an
        //     explicit result — never rethrows. This deliberately replaces
        //     the old Task.WhenAll(tasks) approach, where an exception from
        //     one platform (e.g. a Telegram 429) aborted the call and hid the
        //     fact that the other platform (Discord) may have genuinely sent
        //     the message already. Every result is immediately and fully
        //     logged via CrashLogService — previously only the send INTENT
        //     was logged, not the actual outcome, so the logs couldn't show
        //     that Discord received a message while Telegram didn't (or vice
        //     versa).
        private async Task<PlatformSendResult> RunAsync(
            NotificationPlatform platform, bool shouldAttempt, Func<Task<string?>> send)
        {
            if (!shouldAttempt)
                return new PlatformSendResult { Platform = platform, Attempted = false, Succeeded = false };

            // UA: Фактична відправка і логування розділені навмисно.
            //     Раніше LogInfoAsync/LogErrorAsync були ВСЕРЕДИНІ try/catch
            //     навколо самої відправки — тож (а) якщо лог не вдавалось
            //     записати ПІСЛЯ успішної відправки (диск повний, файл
            //     заблокований), той самий catch хибно позначав платформу як
            //     "не вдалось", хоча повідомлення вже реально пішло, і
            //     (б) LogErrorAsync у catch-блоці сам міг кинути виняток, який
            //     нічим не перехоплювався — тобто RunAsync порушував власну
            //     обіцянку "ніколи не кидає" і Task.WhenAll втрачав результат
            //     другої платформи. Логування тепер — окремий, повністю
            //     ізольований крок, що best-effort і не може змінити чи
            //     зламати вже визначений результат.
            // EN: The actual send and the logging are deliberately split.
            //     Previously LogInfoAsync/LogErrorAsync lived INSIDE the
            //     try/catch around the send itself — so (a) if the log write
            //     failed AFTER a successful send (full disk, locked file),
            //     that same catch incorrectly marked the platform as
            //     "failed" even though the message had genuinely gone out,
            //     and (b) LogErrorAsync inside the catch block could itself
            //     throw, uncaught — meaning RunAsync broke its own "never
            //     throws" promise and Task.WhenAll lost the other platform's
            //     result. Logging is now a separate, fully isolated,
            //     best-effort step that cannot change or break an
            //     already-determined result.
            string? confirmationId = null;
            Exception? error = null;

            try
            {
                confirmationId = await send();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            try
            {
                if (error == null)
                    await _crashLogService.LogInfoAsync(
                        $"[{platform}] відправлено успішно, ID підтвердження: {confirmationId ?? "n/a"}");
                else
                    await _crashLogService.LogErrorAsync($"[{platform}] відправка не вдалась", error);
            }
            catch
            {
                // UA: Збій самого логування — не причина міняти чи ховати
                //     реальний результат відправки
                // EN: A logging failure itself is no reason to change or
                //     hide the actual send result
            }

            return error == null
                ? new PlatformSendResult { Platform = platform, Attempted = true, Succeeded = true, ConfirmationId = confirmationId }
                : new PlatformSendResult { Platform = platform, Attempted = true, Succeeded = false, Error = error };
        }

        // UA: Розсилає на всі увімкнені платформи паралельно, незалежно одна
        //     від одної (див. RunAsync)
        // EN: Sends to all enabled platforms in parallel, independently of
        //     one another (see RunAsync)
        //     discordWebhookUrl — перевизначення Discord-каналу для цієї
        //     конкретної відправки. null означає "канал за замовчуванням", тому
        //     автоматичні сповіщення (де вибору каналу немає) не змінюються.
        //     discordWebhookUrl — a Discord channel override for this specific
        //     send. null means "the default channel", so automatic
        //     notifications (which have no channel picker) are unaffected.
        //     sendToTelegram/sendToDiscord — дозволяють винести конкретну
        //     платформу з ЦІЄЇ спроби відправки без зміни налаштувань
        //     UseTelegram/UseDiscord. Типово true для обох — існуючі викликачі
        //     (автоматичні сповіщення) не змінюються. Призначення:
        //     захист від дублів у SendNotificationWindow — коли платформа вже
        //     підтвердила успішну відправку цього самого вмісту, повторний
        //     клік "Надіслати" не повинен автоматично надсилати туди ще раз.
        //     sendToTelegram/sendToDiscord — let a specific platform be
        //     excluded from THIS send attempt without touching the
        //     UseTelegram/UseDiscord settings. Default true for both —
        //     existing callers (automatic notifications) are unaffected.
        //     Purpose: duplicate-send protection in SendNotificationWindow —
        //     once a platform has already confirmed success for this exact
        //     content, pressing "Send" again shouldn't automatically send
        //     there a second time.
        public async Task<SendResult> SendAsync(VideoInfo video, string? discordWebhookUrl = null,
                                                  bool sendToTelegram = true, bool sendToDiscord = true)
        {
            var urlLabel = string.IsNullOrEmpty(video.Url) ? "no-url" : video.Url;
            await _crashLogService.LogInfoAsync($"[{video.Type}] {video.Title} ({urlLabel})");

            var telegramTask = RunAsync(NotificationPlatform.Telegram, _telegramService != null && sendToTelegram,
                async () => (string?)await _telegramService!.SendNotificationAsync(video));
            var discordTask = RunAsync(NotificationPlatform.Discord, _discordService != null && sendToDiscord,
                () => _discordService!.SendNotificationAsync(video, discordWebhookUrl));

            await Task.WhenAll(telegramTask, discordTask);
            return new SendResult { Telegram = telegramTask.Result, Discord = discordTask.Result };
        }

        // UA: Розсилка у повністю ручному режимі — без шаблону, кожна
        //     платформа отримує свій текст незалежно. Порожній/пробільний
        //     текст для платформи означає "не відправляти туди" — це
        //     навмисно (наприклад, коли анонс потрібен лише в Discord)
        // EN: Fully manual dispatch — no template, each platform gets its
        //     own text independently. An empty/whitespace text for a
        //     platform means "don't send there" — this is intentional (e.g.
        //     when the announcement is only needed on Discord)
        //     imagePaths — необов'язкові локальні зображення, спільні для
        //     обох платформ (ручний режим). Правило "порожній текст = не
        //     відправляти туди" лишається чинним і для зображень — вкладення
        //     їдуть лише разом із текстом, а не замість нього. Це свідомо:
        //     інакше порожнє поле Telegram/Discord перестало б означати
        //     "сюди не надсилати", якщо галерея не порожня.
        //     imagePaths — optional local images shared by both platforms
        //     (manual mode). The "empty text = don't send there" rule still
        //     applies with images — attachments ride along with the text,
        //     they don't replace it. This is deliberate: otherwise an empty
        //     Telegram/Discord field would stop meaning "don't send here"
        //     whenever the gallery isn't empty.
        //     Повертає SendResult — окремий, явний результат для кожної
        //     платформи (див. RunAsync), а не "усе або нічого".
        //     Returns a SendResult — a separate, explicit result for each
        //     platform (see RunAsync), not "all or nothing".
        //     sendToTelegram/sendToDiscord — див. коментар у SendAsync вище;
        //     той самий механізм захисту від дублів застосовується тут.
        //     sendToTelegram/sendToDiscord — see the comment in SendAsync
        //     above; the same duplicate-send protection applies here.
        public async Task<SendResult> SendRawAsync(string? telegramText, string? discordText,
                                       string? discordWebhookUrl = null,
                                       IReadOnlyList<string>? imagePaths = null,
                                       bool sendToTelegram = true, bool sendToDiscord = true)
        {
            var telegramRequested = sendToTelegram && !string.IsNullOrWhiteSpace(telegramText);
            var discordRequested = sendToDiscord && !string.IsNullOrWhiteSpace(discordText);

            await _crashLogService.LogInfoAsync(
                $"[RAW] telegram={(telegramRequested ? "yes" : "no")}, discord={(discordRequested ? "yes" : "no")}, images={imagePaths?.Count ?? 0}");

            var telegramTask = RunAsync(NotificationPlatform.Telegram, _telegramService != null && telegramRequested,
                () => _telegramService!.SendRawAsync(telegramText!, imagePaths));
            var discordTask = RunAsync(NotificationPlatform.Discord, _discordService != null && discordRequested,
                () => _discordService!.SendRawAsync(discordText!, discordWebhookUrl, imagePaths));

            await Task.WhenAll(telegramTask, discordTask);
            return new SendResult { Telegram = telegramTask.Result, Discord = discordTask.Result };
        }

        // UA: Перевіряє, чи дійсно працюють налаштовані Telegram-токен і
        //     Discord-вебхук, БЕЗ фактичної відправки повідомлення — щоб
        //     можливу проблему зі з'єднанням було видно ще до натискання
        //     "Надіслати", а не постфактум. Повертає той самий SendResult,
        //     що й реальна відправка (Attempted/Succeeded/Error по кожній
        //     платформі) — окремого типу для цього не потрібно, семантика
        //     "чи вдалось достукатись до платформи" та сама.
        //     discordWebhookUrl — той самий сенс, що й у SendAsync/
        //     SendRawAsync: перевіряється саме той канал, який зараз обрано
        //     у вікні надсилання, а не лише типовий.
        // EN: Checks whether the configured Telegram token and Discord
        //     webhook actually work, WITHOUT sending an actual message — so
        //     a connectivity problem is visible before "Send" is pressed,
        //     not after the fact. Returns the same SendResult type as an
        //     actual send (Attempted/Succeeded/Error per platform) — a
        //     dedicated type isn't needed, the semantics of "was the
        //     platform reachable" are the same.
        //     discordWebhookUrl — same meaning as in SendAsync/SendRawAsync:
        //     checks the channel currently selected in the send window, not
        //     just the default one.
        public async Task<SendResult> CheckConnectionsAsync(string? discordWebhookUrl = null)
        {
            var telegramTask = CheckOneAsync(NotificationPlatform.Telegram, _telegramService != null,
                () => _telegramService!.CheckConnectionAsync());
            var discordTask = CheckOneAsync(NotificationPlatform.Discord, _discordService != null,
                () => _discordService!.CheckConnectionAsync(discordWebhookUrl));

            await Task.WhenAll(telegramTask, discordTask);
            return new SendResult { Telegram = telegramTask.Result, Discord = discordTask.Result };
        }

        // UA: Окремий, навмисно "тихий" близнюк RunAsync — без логування
        //     через CrashLogService. Перевірка з'єднання — не відправка, тож
        //     писати в logs/ рядки на кшталт "відправка не вдалась" для
        //     простого "чи дійсний токен" було б оманливим, коли ці логи
        //     читатимуть пізніше (виглядало б як реальна невдала спроба
        //     відправити сповіщення).
        // EN: A separate, deliberately "quiet" twin of RunAsync — no
        //     CrashLogService logging. A connection check isn't a send, so
        //     writing lines like "send failed" to logs/ for a simple
        //     "is the token even valid" check would be misleading when the
        //     logs are read later (it would look like a real failed attempt
        //     to send a notification).
        private static async Task<PlatformSendResult> CheckOneAsync(
            NotificationPlatform platform, bool shouldAttempt, Func<Task<(bool Ok, string? Error)>> check)
        {
            if (!shouldAttempt)
                return new PlatformSendResult { Platform = platform, Attempted = false, Succeeded = false };

            try
            {
                var (ok, error) = await check();
                return ok
                    ? new PlatformSendResult { Platform = platform, Attempted = true, Succeeded = true }
                    : new PlatformSendResult
                    {
                        Platform = platform, Attempted = true, Succeeded = false,
                        Error = new InvalidOperationException(error ?? "unknown error")
                    };
            }
            catch (Exception ex)
            {
                return new PlatformSendResult { Platform = platform, Attempted = true, Succeeded = false, Error = ex };
            }
        }
    }
}