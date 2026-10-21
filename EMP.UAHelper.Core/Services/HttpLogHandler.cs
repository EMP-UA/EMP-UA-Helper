// =============================================================================
// EMP UA Helper — HttpLogHandler.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Діагностичний HTTP-обробник для Telegram і Discord. Логує КОЖЕН
//     фактичний HTTP-запит — включно з автоматичними повторами бібліотеки
//     Telegram.Bot (RetryCount/RetryThreshold), невидимими для коду
//     програми. Для кожного запиту в лог потрапляють: ціль (метод API чи
//     вебхук), час початку, тривалість, HTTP-статус, заголовки
//     rate-limit, тіло JSON-запиту й тіло відповіді з помилкою.
//     Секрети (токен бота, токен вебхука) НІКОЛИ не потрапляють у лог:
//     повний URL не логується — лише його безпечний опис, а будь-який
//     текст додатково проходить через функцію очищення перед записом.
// EN: Diagnostic HTTP handler for Telegram and Discord. Logs EVERY actual
//     HTTP request — including the Telegram.Bot library's automatic retries
//     (RetryCount/RetryThreshold), invisible to the application code. For
//     each request the log gets: the target (API method or webhook), start
//     time, duration, HTTP status, rate-limit headers, the JSON request
//     body and the error response body.
//     Secrets (bot token, webhook token) NEVER reach the log: the full URL
//     isn't logged — only a safe description of it — and any text
//     additionally goes through the scrubbing function before writing.
// =============================================================================
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace EMP.UAHelper.Core.Services
{
    public sealed partial class HttpLogHandler : DelegatingHandler
    {
        // UA: Обмеження довжини тіла в одному рядку логу
        // EN: Body length cap per log line
        private const int MaxBodyLogLength = 2000;

        // UA: Заголовки rate-limit, які варто бачити в лозі. Discord надсилає
        //     X-RateLimit-* у кожній відповіді, Retry-After — при 429;
        //     Telegram передає retry_after у тілі відповіді
        // EN: Rate-limit headers worth seeing in the log. Discord sends
        //     X-RateLimit-* in every response and Retry-After on a 429;
        //     Telegram passes retry_after in the response body
        private static readonly string[] RateLimitHeaders =
        {
            "Retry-After",
            "X-RateLimit-Limit",
            "X-RateLimit-Remaining",
            "X-RateLimit-Reset-After",
            "X-RateLimit-Scope",
            "X-RateLimit-Global"
        };

        private readonly CrashLogService _log;
        private readonly string _tag;
        private readonly Func<Uri?, string> _describeTarget;
        private readonly Func<string, string> _redact;

        private HttpLogHandler(CrashLogService log, string tag,
            Func<Uri?, string> describeTarget, Func<string, string> redact)
            : base(new HttpClientHandler())
        {
            _log = log;
            _tag = tag;
            _describeTarget = describeTarget;
            _redact = redact;
        }

        // UA: Telegram: URL має вигляд /bot<токен>/<метод> — у лог іде лише
        //     назва методу, а токен вирізається з будь-якого тексту
        // EN: Telegram: the URL looks like /bot<token>/<method> — only the
        //     method name goes to the log, and the token is cut from any text
        public static HttpLogHandler ForTelegram(CrashLogService log, string botToken)
            => new(log, "TG-HTTP",
                uri =>
                {
                    var last = uri?.Segments.LastOrDefault();
                    return string.IsNullOrEmpty(last) ? "?" : last.Trim('/');
                },
                text => string.IsNullOrEmpty(botToken) ? text : text.Replace(botToken, "***"));

        // UA: Discord: URL має вигляд /api/webhooks/<id>/<токен>. У лог іде
        //     лише "webhook …<останні 4 цифри id>" (щоб розрізняти канали) і
        //     query-рядок; токен вебхука вирізається з будь-якого тексту
        // EN: Discord: the URL looks like /api/webhooks/<id>/<token>. Only
        //     "webhook …<last 4 digits of id>" (to tell channels apart) and the
        //     query string go to the log; the webhook token is cut from any text
        public static HttpLogHandler ForDiscord(CrashLogService log)
            => new(log, "DC-HTTP",
                uri =>
                {
                    if (uri == null) return "?";
                    var match = DiscordWebhookPath().Match(uri.AbsolutePath);
                    if (!match.Success) return "?";
                    var id = match.Groups["id"].Value;
                    var shortId = id.Length > 4 ? id[^4..] : id;
                    return $"webhook …{shortId}{uri.Query}";
                },
                text => DiscordWebhookPath().Replace(text, "/api/webhooks/***"));

        [GeneratedRegex(@"/api/webhooks/(?<id>\d+)/[A-Za-z0-9_\-\.]+")]
        private static partial Regex DiscordWebhookPath();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var target = _describeTarget(request.RequestUri);
            var requestBody = await ReadLoggableBodyAsync(request.Content);

            await SafeLogAsync(false, $"[{_tag}] → {request.Method} {target} {requestBody}");

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await SafeLogAsync(true,
                    $"[{_tag}] ✗ {target}: мережевий виняток / network exception, {stopwatch.ElapsedMilliseconds} ms — {ex.GetType().Name}: {ex.Message}");
                throw;
            }

            stopwatch.Stop();
            var summary = $"[{_tag}] ← {target}: HTTP {(int)response.StatusCode}, {stopwatch.ElapsedMilliseconds} ms{FormatRateLimitHeaders(response)}";

            if (response.IsSuccessStatusCode)
            {
                await SafeLogAsync(false, summary);
            }
            else
            {
                var responseBody = await ReadLoggableBodyAsync(response.Content);
                await SafeLogAsync(true, $"{summary} — {responseBody}");
            }

            return response;
        }

        private static string FormatRateLimitHeaders(HttpResponseMessage response)
        {
            var parts = new List<string>();
            foreach (var name in RateLimitHeaders)
            {
                if (response.Headers.TryGetValues(name, out var values))
                    parts.Add($"{name}={string.Join(",", values)}");
            }

            return parts.Count == 0 ? string.Empty : $" [{string.Join("; ", parts)}]";
        }

        // UA: JSON і текст читаються з буфера (повторне читання бібліотекою
        //     лишається можливим); multipart (зображення) не читається —
        //     у лог іде лише тип і розмір
        // EN: JSON and text are read from a buffer (the library can still
        //     read them again); multipart (images) isn't read — only the type
        //     and size go to the log
        private async Task<string> ReadLoggableBodyAsync(HttpContent? content)
        {
            if (content == null) return "(без тіла / no body)";

            try
            {
                var mediaType = content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!mediaType.Contains("json") && !mediaType.StartsWith("text/"))
                    return $"({mediaType}, {content.Headers.ContentLength?.ToString() ?? "?"} B)";

                await content.LoadIntoBufferAsync();
                var body = _redact(await content.ReadAsStringAsync());
                return body.Length > MaxBodyLogLength
                    ? body[..MaxBodyLogLength] + "…"
                    : body;
            }
            catch (Exception ex)
            {
                return $"(тіло не прочитано / body not read: {ex.GetType().Name})";
            }
        }

        // UA: Збій логування не повинен впливати на сам запит
        // EN: A logging failure must not affect the request itself
        private async Task SafeLogAsync(bool isWarning, string message)
        {
            try
            {
                var clean = _redact(message);
                if (isWarning) await _log.LogWarningAsync(clean);
                else await _log.LogInfoAsync(clean);
            }
            catch
            {
            }
        }
    }
}
