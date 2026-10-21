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
        //     результат — ніколи не кидає далі, тож виняток однієї платформи
        //     (напр. Telegram 429) не ховає, що друга платформа (Discord)
        //     могла вже реально відправити повідомлення. Кожен результат
        //     одразу й повністю логується через CrashLogService — у логах
        //     видно фактичний результат по кожній платформі.
        // EN: Runs a send attempt for one platform and ALWAYS returns an
        //     explicit result — never rethrows, so an exception from one
        //     platform (e.g. a Telegram 429) doesn't hide the fact that the
        //     other platform (Discord) may have genuinely sent the message
        //     already. Every result is immediately and fully logged via
        //     CrashLogService — the logs show the actual outcome per
        //     platform.
        private async Task<PlatformSendResult> RunAsync(
            NotificationPlatform platform, bool shouldAttempt, Func<Task<string?>> send,
            Func<string?>? getNote = null)
        {
            if (!shouldAttempt)
                return new PlatformSendResult { Platform = platform, Attempted = false, Succeeded = false };

            // UA: Фактична відправка і логування розділені навмисно.
            //     Якби LogInfoAsync/LogErrorAsync були ВСЕРЕДИНІ try/catch
            //     навколо самої відправки, то (а) збій запису логу ПІСЛЯ
            //     успішної відправки (диск повний, файл заблокований) хибно
            //     позначив би платформу як "не вдалось", хоча повідомлення
            //     вже реально пішло, і (б) виняток із LogErrorAsync у
            //     catch-блоці порушив би обіцянку "ніколи не кидає".
            //     Логування — окремий, повністю ізольований крок, що
            //     best-effort і не може змінити чи зламати вже визначений
            //     результат.
            // EN: The actual send and the logging are deliberately split.
            //     Had LogInfoAsync/LogErrorAsync lived INSIDE the try/catch
            //     around the send itself, then (a) a log write failure AFTER
            //     a successful send (full disk, locked file) would wrongly
            //     mark the platform as "failed" even though the message had
            //     genuinely gone out, and (b) an exception from LogErrorAsync
            //     inside the catch block would break the "never throws"
            //     promise. Logging is a separate, fully isolated, best-effort
            //     step that cannot change or break an already-determined
            //     result.
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
                ? new PlatformSendResult { Platform = platform, Attempted = true, Succeeded = true, ConfirmationId = confirmationId, Note = getNote?.Invoke() }
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
        //     telegramChatId — перевизначення чату Telegram (тестова
        //     відправка в особистий чат); null — канал із налаштувань.
        //     isTest — позначка [TEST] у лозі.
        //     telegramChatId — a Telegram chat override (test send to a
        //     private chat); null means the channel from settings.
        //     isTest — a [TEST] mark in the log.
        public async Task<SendResult> SendAsync(VideoInfo video, string? discordWebhookUrl = null,
                                                  bool sendToTelegram = true, bool sendToDiscord = true,
                                                  string? telegramChatId = null, bool isTest = false)
        {
            var urlLabel = string.IsNullOrEmpty(video.Url) ? "no-url" : video.Url;
            await _crashLogService.LogInfoAsync($"{TestMark(isTest)}[{video.Type}] {video.Title} ({urlLabel})");

            string? telegramNote = null;
            var telegramTask = RunAsync(NotificationPlatform.Telegram, _telegramService != null && sendToTelegram,
                async () => (string?)await _telegramService!.SendNotificationAsync(video, telegramChatId, n => telegramNote = n),
                () => telegramNote);
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
                                       bool sendToTelegram = true, bool sendToDiscord = true,
                                       string? telegramChatId = null, bool isTest = false)
        {
            var telegramRequested = sendToTelegram && !string.IsNullOrWhiteSpace(telegramText);
            var discordRequested = sendToDiscord && !string.IsNullOrWhiteSpace(discordText);

            await _crashLogService.LogInfoAsync(
                $"{TestMark(isTest)}[RAW] telegram={(telegramRequested ? "yes" : "no")}, discord={(discordRequested ? "yes" : "no")}, images={imagePaths?.Count ?? 0}");

            var telegramTask = RunAsync(NotificationPlatform.Telegram, _telegramService != null && telegramRequested,
                () => _telegramService!.SendRawAsync(telegramText!, imagePaths, telegramChatId));
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
        //     telegramChatId/checkTelegram/checkDiscord — для тестового
        //     режиму: перевіряється тестовий чат, а платформа без тестової
        //     цілі не перевіряється зовсім.
        //     telegramChatId/checkTelegram/checkDiscord — for test mode: the
        //     test chat is checked, and a platform without a test target
        //     isn't checked at all.
        public async Task<SendResult> CheckConnectionsAsync(string? discordWebhookUrl = null,
                                                            string? telegramChatId = null,
                                                            bool checkTelegram = true, bool checkDiscord = true)
        {
            var telegramTask = CheckOneAsync(NotificationPlatform.Telegram, _telegramService != null && checkTelegram,
                () => _telegramService!.CheckConnectionAsync(telegramChatId));
            var discordTask = CheckOneAsync(NotificationPlatform.Discord, _discordService != null && checkDiscord,
                () => _discordService!.CheckConnectionAsync(discordWebhookUrl));

            await Task.WhenAll(telegramTask, discordTask);
            return new SendResult { Telegram = telegramTask.Result, Discord = discordTask.Result };
        }

        private static string TestMark(bool isTest) => isTest ? "[TEST] " : string.Empty;

        // UA: Окремий, навмисно "тихий" близнюк RunAsync — без рядків
        //     "відправлено"/"відправка не вдалась". Перевірка з'єднання — не
        //     відправка, тож такі рядки в logs/ виглядали б як реальна спроба
        //     відправити сповіщення. Самі HTTP-запити getMe/getChat і GET вебхука при цьому
        //     логуються як діагностика [TG-HTTP]/[DC-HTTP] (див. HttpLogHandler).
        // EN: A separate, deliberately "quiet" twin of RunAsync — no
        //     "sent"/"send failed" lines. A connection check isn't a send, so
        //     such lines in logs/ would look like a real attempt to send a
        //     notification. The getMe/getChat and webhook GET HTTP requests are
        //     still logged as [TG-HTTP]/[DC-HTTP] diagnostics (see HttpLogHandler).
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