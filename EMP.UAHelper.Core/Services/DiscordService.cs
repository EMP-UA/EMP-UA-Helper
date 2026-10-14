// =============================================================================
// EMP UA Helper — DiscordService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для відправки сповіщень у Discord через вебхук
// EN: Service for sending notifications to Discord via webhook
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EMP.UAHelper.Core.Services
{
    public class DiscordService
    {
        private readonly string _webhookUrl;
        private readonly string _twitchUrl;
        private readonly string _roleId;
        private readonly TemplateService _templateService;

        // UA: Див. коментар до аналогічного поля в TelegramService — той
        //     самий підхід, той самий AppTimeZone, без хардкоду
        // EN: See the comment on the equivalent field in TelegramService —
        //     same approach, same AppTimeZone, no hardcoding
        private readonly TimeZoneInfo _timeZone;

        // UA: Формати згадок Discord. Винесені сюди, бо синтаксис у трьох
        //     типів різний рівно однією позначкою ("&" і "#"), і саме на цьому
        //     найлегше помилитись, набираючи вручну. Тримати їх в одному місці
        //     означає, що і генератор у вікні надсилання, і автоматичний пінг
        //     ролі нижче користуються тим самим кодом.
        // EN: Discord mention formats. Kept here because the syntax of the
        //     three types differs by exactly one character ("&" and "#"), which
        //     is precisely what's easiest to get wrong when typing by hand.
        //     Having them in one place means both the generator in the send
        //     window and the automatic role ping below use the same code.
        public enum MentionKind
        {
            Role,
            User,
            Channel
        }

        // UA: Побудувати тег згадки з "голого" ID. Повертає порожній рядок для
        //     порожнього/некоректного ID, щоб у повідомлення не потрапив
        //     обрізаний тег на кшталт "<@&>", який Discord покаже як текст
        // EN: Build a mention tag from a "bare" ID. Returns an empty string for
        //     an empty/invalid ID, so no truncated tag like "<@&>" — which
        //     Discord would render as plain text — ends up in the message
        public static string FormatMention(MentionKind kind, string? id)
        {
            var trimmed = id?.Trim() ?? string.Empty;

            // UA: ID у Discord — це snowflake, тобто виключно цифри ASCII 0-9.
            //     Перевірка дешева, а рятує від найтиповішого випадку: людина
            //     копіює з Discord готовий тег "<@&123>" замість самого ID, і
            //     без перевірки ми б згенерували "<@&<@&123>>".
            //     Саме IsAsciiDigit, а не IsDigit: IsDigit вважає цифрою будь-який
            //     символ Unicode-категорії Nd, тобто пропустив би, наприклад,
            //     арабо-індійські "١٢٣" — вони виглядають як число, але Discord
            //     такий тег не розпізнає і покаже його як звичайний текст.
            // EN: A Discord ID is a snowflake, i.e. ASCII digits 0-9 only. The
            //     check is cheap and guards the most typical case: the person
            //     copies a ready tag "<@&123>" out of Discord instead of the
            //     bare ID, and without the check we'd generate "<@&<@&123>>".
            //     IsAsciiDigit specifically, not IsDigit: IsDigit treats any
            //     Unicode category Nd character as a digit, so it would let
            //     through e.g. Arabic-Indic "١٢٣" — which looks like a number
            //     but Discord won't recognize the tag and will render it as text.
            if (trimmed.Length == 0 || !trimmed.All(char.IsAsciiDigit))
                return string.Empty;

            return kind switch
            {
                MentionKind.Role => $"<@&{trimmed}>",
                MentionKind.User => $"<@{trimmed}>",
                MentionKind.Channel => $"<#{trimmed}>",
                _ => string.Empty
            };
        }

        // UA: Кольори embed повідомлень для різних типів контенту (десяткове RGB)
        // EN: Embed message colors for different content types (decimal RGB)
        private const int ColorLive = 0xFF0000; // UA: Червоний / EN: Red
        private const int ColorUpcoming = 0xFFA500; // UA: Помаранчевий / EN: Orange
        private const int ColorVideo = 0x8A46C1; // UA: Фіолетовий EMP / EN: EMP Purple
        private const int ColorShort = 0xC989F3; // UA: Світло-фіолетовий EMP / EN: EMP Light Purple

        public DiscordService(string webhookUrl, string twitchUrl, string roleId,
            string timeZoneId, TemplateService templateService)
        {
            _webhookUrl = webhookUrl;
            _twitchUrl = twitchUrl;
            _roleId = roleId;
            _timeZone = AppTimeZone.Resolve(timeZoneId);
            _templateService = templateService;
        }

        // UA: Відправити embed сповіщення залежно від типу контенту
        // EN: Send embed notification depending on content type
        //     webhookUrl — необов'язкове перевизначення каналу. Порожнє
        //     значення означає "канал за замовчуванням з налаштувань", тому
        //     наявні виклики (автоматичні сповіщення) працюють без змін.
        //     webhookUrl — an optional channel override. An empty value means
        //     "the default channel from settings", so existing call sites
        //     (automatic notifications) keep working unchanged.
        //     Повертає ID створеного повідомлення Discord (підтвердження
        //     реальної відправки — Discord Webhook за замовчуванням поводиться
        //     як fire-and-forget і нічого не підтверджує) або null, якщо канал
        //     не налаштований (сповіщення не відправлялось взагалі).
        //     Returns the created Discord message's ID (confirmation of an
        //     actual send — Discord webhooks default to fire-and-forget
        //     behavior and confirm nothing) or null if no channel is
        //     configured (nothing was sent at all).
        public async Task<string?> SendNotificationAsync(VideoInfo video, string? webhookUrl = null)
        {
            var templates = _templateService.GetTemplates();
            var bodyTemplates = templates.Discord;
            var titleTemplates = templates.DiscordTitles;

            // UA: Обираємо шаблони залежно від типу контенту
            // EN: Select templates based on content type
            var (titleTemplate, bodyTemplate, color) = video.Type switch
            {
                VideoType.Live =>
                    (titleTemplates.Live, bodyTemplates.Live, ColorLive),
                VideoType.Upcoming =>
                    (titleTemplates.Upcoming, bodyTemplates.Upcoming, ColorUpcoming),
                VideoType.Video =>
                    (titleTemplates.Video, bodyTemplates.Video, ColorVideo),
                VideoType.Short =>
                    (titleTemplates.Short, bodyTemplates.Short, ColorShort),
                _ => throw new ArgumentOutOfRangeException()
            };

            // UA: Передаємо ScheduledStartTime для підстановки Unix timestamp у шаблон
            // EN: Pass ScheduledStartTime for Unix timestamp substitution in template
            var embedTitle = _templateService.Apply(
                titleTemplate,
                video.Title,
                video.Url,
                _twitchUrl,
                _timeZone,
                video.ScheduledStartTime);

            var description = _templateService.Apply(
                bodyTemplate,
                video.Title,
                video.Url,
                _twitchUrl,
                _timeZone,
                video.ScheduledStartTime);

            // UA: Embed будується через Dictionary, щоб мати змогу умовно пропустити
            //     image, якщо превʼю немає (ручне сповіщення без YouTube)
            // EN: The embed is built via Dictionary to allow conditionally skipping
            //     the image when there's no thumbnail (manual notification without YouTube)
            var embed = new Dictionary<string, object>
            {
                ["title"] = embedTitle,
                ["description"] = description,
                ["color"] = color,
                // UA: Підпис embed в тематиці EMP
                // EN: Embed footer in EMP theme
                ["footer"] = new { text = "Silence will fall." }
            };

            if (!string.IsNullOrEmpty(video.ThumbnailUrl))
                embed["image"] = new { url = video.ThumbnailUrl };

            var payload = new Dictionary<string, object>
            {
                ["embeds"] = new[] { embed }
            };

            // UA: Пінг ролі додаємо лише якщо RoleId заданий
            // EN: Add the role mention only if RoleId is set
            var roleMention = FormatMention(MentionKind.Role, _roleId);
            if (roleMention.Length > 0)
                payload["content"] = roleMention;

            return await PostAsync(payload, webhookUrl);
        }

        // UA: Повністю ручна відправка — без TemplateService і без embed,
        //     звичайне повідомлення ("content"). Discord сам розпізнає
        //     markdown-форматування (**жирний**, *курсив*, __підкреслення__,
        //     ~~закреслення~~, `код`) і теги (<@&roleId> — роль, <@userId> —
        //     користувач, <#channelId> — канал, @everyone/@here), якщо вони
        //     присутні прямо в тексті — жодного додаткового коду для цього не
        //     потрібно. Ліміт Discord на довжину content — 2000 символів
        // EN: Fully manual send — bypasses TemplateService and the embed,
        //     a plain message ("content"). Discord natively recognizes
        //     markdown formatting (**bold**, *italic*, __underline__,
        //     ~~strikethrough~~, `code`) and tags (<@&roleId> — role,
        //     <@userId> — user, <#channelId> — channel, @everyone/@here) when
        //     they're present directly in the text — no extra code needed.
        //     Discord's content length limit is 2000 characters
        //     imagePaths — необов'язкові локальні файли зображень для
        //     прикріплення (ручний режим, вибираються через провідник).
        //     null/порожній список — поведінка не змінюється, той самий
        //     JSON-запит, що й раніше.
        //     imagePaths — optional local image files to attach (manual mode,
        //     picked via the file browser). null/empty means unchanged
        //     behavior — the same plain JSON request as before.
        public async Task<string?> SendRawAsync(string text, string? webhookUrl = null,
                                        IReadOnlyList<string>? imagePaths = null)
        {
            var hasImages = imagePaths is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(text) && !hasImages) return null;

            if (!hasImages)
            {
                var payload = new Dictionary<string, object> { ["content"] = text };
                return await PostAsync(payload, webhookUrl);
            }

            return await PostWithAttachmentsAsync(text, imagePaths!, webhookUrl);
        }

        // UA: Скільки файлів можна прикріпити до одного повідомлення. Discord
        //     сам дозволяє більше, але це число навмисно узгоджене з
        //     Telegram-обмеженням на медіа-групу (2-10) — той самий набір
        //     файлів у ручному режимі йде в обидві платформи одночасно, і
        //     єдина межа в UI зрозуміліша, ніж дві різні.
        // EN: How many files can be attached to a single message. Discord
        //     itself allows more, but this number is deliberately aligned
        //     with Telegram's media-group limit (2-10) — the same set of
        //     files goes to both platforms at once in manual mode, and one
        //     shared UI limit is clearer than two different ones.
        public const int MaxAttachmentsPerMessage = 10;

        // UA: Multipart-запит з вкладеннями. Discord Webhook API вимагає
        //     саме такої форми (не звичайний JSON) для файлів: текстова
        //     частина йде в полі "payload_json", кожен файл — в окремому полі
        //     "files[N]" (N з нуля). Потоки читання файлів тримаємо відкритими
        //     до завершення запиту й закриваємо в finally — HttpClient читає
        //     їх лінькво під час відправки, а не наперед у пам'ять.
        // EN: A multipart request with attachments. The Discord Webhook API
        //     requires exactly this shape (not plain JSON) for files: the
        //     text part goes in the "payload_json" field, each file in its
        //     own "files[N]" field (N starting at zero). File streams are
        //     kept open until the request completes and disposed in finally —
        //     HttpClient reads them lazily while sending, not all into memory upfront.
        private async Task<string?> PostWithAttachmentsAsync(string text, IReadOnlyList<string> imagePaths, string? webhookUrl)
        {
            var target = string.IsNullOrWhiteSpace(webhookUrl) ? _webhookUrl : webhookUrl;

            // UA: Порожній URL тут — це вже НЕ "нічого надсилати" (той випадок
            //     відфільтрований викликачем ще до цього методу), а зіпсована
            //     конфігурація: платформа мала отримати повідомлення, але
            //     немає куди. Раніше тут був мовчазний "return null", який
            //     RunAsync у NotificationDispatcher сприймав як УСПІХ — саме
            //     того виду мовчазної "відправки в нікуди" ця ціла доробка
            //     покликана усунути, тож тепер це явна помилка.
            // EN: An empty URL here is NOT "nothing to send" (that case is
            //     already filtered by the caller before this method runs) —
            //     it's a broken configuration: the platform was supposed to
            //     receive the message but has nowhere to send it. This used
            //     to be a silent "return null", which NotificationDispatcher's
            //     RunAsync treated as SUCCESS — exactly the kind of silent
            //     "sent into the void" this whole effort exists to eliminate,
            //     so this is now an explicit failure.
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException(
                    "Discord: webhook URL не налаштовано / webhook URL is not configured.");

            var files = imagePaths.Take(MaxAttachmentsPerMessage).ToList();
            var payloadJson = JsonSerializer.Serialize(new Dictionary<string, object> { ["content"] = text });

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

            var streams = new List<FileStream>();
            try
            {
                for (var i = 0; i < files.Count; i++)
                {
                    var stream = File.OpenRead(files[i]);
                    streams.Add(stream);

                    var streamContent = new StreamContent(stream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(GuessImageMimeType(files[i]));
                    form.Add(streamContent, $"files[{i}]", Path.GetFileName(files[i]));
                }

                // UA: "?wait=true" змушує Discord повернути реальний статус і
                //     JSON створеного повідомлення замість fire-and-forget
                //     204 без тіла — без цього дізнатись, чи повідомлення
                //     справді дійшло, було б неможливо (саме це й стало причиною
                //     дублікатів: користувач тиснув кнопку повторно, бо
                //     застосунок за замовчуванням вважав відправку успішною)
                // EN: "?wait=true" forces Discord to return the real status and
                //     the created message's JSON instead of a fire-and-forget
                //     204 with no body — without it, whether the message
                //     actually arrived would NEVER be known (this is exactly what caused
                //     the duplicates: the user kept pressing send because the
                //     app silently assumed success by default)
                using var response = await Http.PostAsync(WithWaitParam(target), form);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Discord: {BuildErrorMessage(response.StatusCode, body)}");

                return ExtractMessageId(body);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        // UA: MIME-тип за розширенням файлу — цього достатньо, бо файли сюди
        //     потрапляють лише через діалог вибору файлу з фільтром за
        //     зображеннями (SendNotificationWindow), а не з довільного джерела
        // EN: MIME type from the file extension — sufficient here, since files
        //     only reach this point via the image-filtered file picker dialog
        //     (SendNotificationWindow), not from an arbitrary source
        private static string GuessImageMimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };

        // UA: Єдина точка відправки в Discord. Раніше і embed, і ручне
        //     повідомлення дублювали серіалізацію та створення HttpClient —
        //     тепер вибір каналу (перевизначення чи типовий) реалізований
        //     рівно один раз, тож ці два шляхи не можуть розійтись у поведінці.
        //     HttpClient статичний з тієї ж причини, що й у YouTubeService:
        //     новий екземпляр на кожну відправку лишає TCP-з'єднання висіти.
        // EN: The single Discord send point. Previously both the embed and the
        //     manual message duplicated serialization and HttpClient creation —
        //     now channel selection (override or default) is implemented
        //     exactly once, so the two paths can't drift apart in behavior.
        //     The HttpClient is static for the same reason as in YouTubeService:
        //     a new instance per send leaves TCP connections lingering.
        private static readonly HttpClient Http = new();

        private async Task<string?> PostAsync(Dictionary<string, object> payload, string? webhookUrl)
        {
            var target = string.IsNullOrWhiteSpace(webhookUrl) ? _webhookUrl : webhookUrl;

            // UA: Див. коментар до аналогічної перевірки в
            //     PostWithAttachmentsAsync — та сама причина
            // EN: See the comment on the equivalent check in
            //     PostWithAttachmentsAsync — the same reasoning
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException(
                    "Discord: webhook URL не налаштовано / webhook URL is not configured.");

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // UA: Див. коментар до аналогічного рядка в PostWithAttachmentsAsync —
            //     "?wait=true" тут обов'язковий з тієї ж причини
            // EN: See the comment on the equivalent line in PostWithAttachmentsAsync —
            //     "?wait=true" is mandatory here for the same reason
            using var response = await Http.PostAsync(WithWaitParam(target), content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Discord: {BuildErrorMessage(response.StatusCode, body)}");

            return ExtractMessageId(body);
        }

        // UA: Додає "?wait=true" до URL вебхука. Discord webhook API за
        //     замовчуванням поводиться як fire-and-forget — повертає 204
        //     без тіла одразу, навіть не чекаючи, поки повідомлення реально
        //     створено. "?wait=true" перемикає його в синхронний режим:
        //     відповідь приходить лише після фактичного створення
        //     повідомлення й містить його JSON (включно з "id") або помилку.
        //     Без query-рядка додається "?", з уже наявним — "&", щоб не
        //     зламати URL, якщо webhookUrl колись міститиме інші параметри.
        // EN: Appends "?wait=true" to the webhook URL. The Discord webhook API
        //     defaults to fire-and-forget behavior — it returns a 204 with no
        //     body immediately, without even waiting for the message to
        //     actually be created. "?wait=true" switches it to synchronous
        //     mode: the response only arrives after the message is actually
        //     created and contains its JSON (including "id"), or the real
        //     error. Appends
        //     "?" when there's no existing query string, "&" otherwise, so the
        //     URL doesn't break if webhookUrl ever carries other parameters.
        private static string WithWaitParam(string url) =>
            url.Contains('?') ? $"{url}&wait=true" : $"{url}?wait=true";

        // UA: Будує читабельне повідомлення про помилку з відповіді Discord.
        //     Discord повертає JSON вигляду {"message": "...", "code": N} або,
        //     для 429, ще й "retry_after" у секундах (саме цей випадок
        //     користувач бачив як голий текст "Too Many Requests: retry after
        //     8" у стандартному діалозі помилки .NET — тепер замість цього тут
        //     формується зрозуміле повідомлення з конкретним часом очікування).
        //     Якщо тіло не є очікуваним JSON (напр. HTML-сторінка помилки
        //     проксі), повертаємось до сирого тіла, аби не загубити інформацію.
        // EN: Builds a human-readable error message from Discord's response.
        //     Discord returns JSON shaped like {"message": "...", "code": N}
        //     or, for 429, also "retry_after" in seconds (this is exactly what
        //     the user saw rendered as the bare text "Too Many Requests: retry
        //     after 8" in .NET's default exception dialog — this now produces
        //     a clear message with the actual wait time instead). If the body
        //     isn't the expected JSON (e.g. a proxy's HTML error page), falls
        //     back to the raw body so no information is lost.
        private static string BuildErrorMessage(HttpStatusCode status, string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var message = root.TryGetProperty("message", out var messageProp)
                    ? messageProp.GetString()
                    : null;
                var retryAfter = root.TryGetProperty("retry_after", out var retryProp)
                    ? retryProp.GetDouble()
                    : (double?)null;

                var text = string.IsNullOrWhiteSpace(message) ? body : message;
                return retryAfter.HasValue
                    ? $"{(int)status} — {text} (повторити через {retryAfter.Value:0.#} с / retry after {retryAfter.Value:0.#}s)"
                    : $"{(int)status} — {text}";
            }
            catch (Exception)
            {
                // UA: Будь-яка проблема розбору (не лише невалідний JSON, а й,
                //     наприклад, несподіваний тип значення в полі) не повинна
                //     ховати сам факт помилки — просто повертається сире тіло
                // EN: Any parsing problem (not just invalid JSON, but also,
                //     say, an unexpected value type in a field) must not hide
                //     the failure itself — just fall back to the raw body
                return string.IsNullOrWhiteSpace(body) ? $"{(int)status}" : $"{(int)status}: {body}";
            }
        }

        // UA: Витягує "id" створеного повідомлення з успішної відповіді
        //     Discord (доступний лише з "?wait=true"). Повертає null, якщо
        //     поле відсутнє чи тіло не є JSON — викликач має сприймати null як
        //     "відправлено, але ID підтвердження недоступне", а не як помилку:
        //     сама відправка на цьому етапі вже відбулась (IsSuccessStatusCode
        //     перевірено раніше).
        // EN: Extracts the created message's "id" from a successful Discord
        //     response (only available with "?wait=true"). Returns null if the
        //     field is missing or the body isn't JSON — the caller should treat
        //     null as "sent, but no confirmation ID available", not as an
        //     error: the send itself already succeeded at this point
        //     (IsSuccessStatusCode was already checked).
        // UA: Перевірка з'єднання БЕЗ фактичної відправки — GET на сам URL
        //     вебхука (не POST). Discord повертає інформацію про вебхук
        //     (200), якщо URL дійсний, або помилку (401/404 — токен
        //     відкликано чи канал видалено), якщо ні. Жодне повідомлення не
        //     публікується, підписники каналу нічого не бачать — це той
        //     самий "тихий" GET-запит, яким будь-хто у Discord міг би
        //     перевірити власний вебхук вручну в браузері.
        // EN: A connection check WITHOUT an actual send — a GET to the
        //     webhook URL itself (not a POST). Discord returns the webhook's
        //     info (200) if the URL is valid, or an error (401/404 — token
        //     revoked or the channel was deleted) if not. No message is
        //     posted, channel subscribers see nothing — this is the same
        //     "silent" GET request anyone on Discord could use to check
        //     their own webhook by hand in a browser.
        public async Task<(bool Ok, string? Error)> CheckConnectionAsync(string? webhookUrl = null)
        {
            var target = string.IsNullOrWhiteSpace(webhookUrl) ? _webhookUrl : webhookUrl;
            if (string.IsNullOrWhiteSpace(target))
                return (false, "Discord: webhook URL не налаштовано / webhook URL is not configured.");

            try
            {
                using var response = await Http.GetAsync(target);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return (false, $"Discord: {BuildErrorMessage(response.StatusCode, body)}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Discord: {ex.Message}");
            }
        }

        private static string? ExtractMessageId(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            }
            catch (Exception)
            {
                // UA: Навмисно перехоплюється БУДЬ-який виняток розбору, не лише
                //     JsonException — це успішний шлях (IsSuccessStatusCode
                //     уже true), тож навіть несподіваний тип значення "id"
                //     не повинен перетворити справжній успіх на хибний "провал"
                // EN: Deliberately catches ANY parsing exception, not just
                //     JsonException — this is the success path
                //     (IsSuccessStatusCode is already true), so even an
                //     unexpected type for "id" must not turn a genuine
                //     success into a false "failure"
                return null;
            }
        }
    }
}