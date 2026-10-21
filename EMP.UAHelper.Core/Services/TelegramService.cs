// =============================================================================
// EMP UA Helper — TelegramService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для відправки сповіщень у Telegram канал
// EN: Service for sending notifications to a Telegram channel
// =============================================================================
using EMP.UAHelper.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace EMP.UAHelper.Core.Services
{
    public class TelegramService
    {
        private readonly TelegramBotClient _botClient;
        private readonly string _channelUsername;
        private readonly string _twitchUrl;
        private readonly TemplateService _templateService;

        // UA: Резолвиться один раз під час побудови сервісу (через AppTimeZone,
        //     без хардкоду) — сервіс і так перестворюється щоразу, коли
        //     змінюються налаштування (див. ContentDispatchFactory), тож
        //     "застаріла" зона тут неможлива
        // EN: Resolved once when the service is built (via AppTimeZone, no
        //     hardcoding) — the service is already recreated on every
        //     settings change (see ContentDispatchFactory), so a "stale"
        //     zone here isn't possible
        private readonly TimeZoneInfo _timeZone;

        private readonly HttpClient _httpClient;
        private readonly CrashLogService _log;

        // UA: true — шаблонне сповіщення одразу надсилається як фото з
        //     мініатюрою YouTube і текстом-підписом, без прев'ю посилання
        // EN: true — a template notification is sent straight away as a photo
        //     with the YouTube thumbnail and the text as its caption, without
        //     a link preview
        private readonly bool _thumbnailAsPhoto;

        // UA: Максимальна довжина підпису до фото в Telegram
        // EN: Telegram's maximum photo caption length
        private const int CaptionLimit = 1024;

        // UA: HttpClient з HttpLogHandler — кожен фактичний HTTP-запит до
        //     Telegram потрапляє в лог. RetryThreshold = 0 вимикає
        //     автоповтори бібліотеки після 429: результат (успіх чи помилка)
        //     повертається одразу, а рішення про повтор лишається за кодом
        //     програми чи людиною
        // EN: An HttpClient with HttpLogHandler — every actual HTTP request to
        //     Telegram is logged. RetryThreshold = 0 disables the library's
        //     auto-retries after a 429: the result (success or error) comes
        //     back immediately, and the retry decision stays with the
        //     application code or the person
        public TelegramService(string botToken, string channelUsername, string twitchUrl,
            string timeZoneId, TemplateService templateService, CrashLogService crashLogService,
            bool thumbnailAsPhoto)
        {
            _httpClient = new HttpClient(HttpLogHandler.ForTelegram(crashLogService, botToken));
            _botClient = new TelegramBotClient(
                new TelegramBotClientOptions(botToken) { RetryThreshold = 0 }, _httpClient);
            _log = crashLogService;
            _thumbnailAsPhoto = thumbnailAsPhoto;
            _channelUsername = channelUsername;
            _twitchUrl = twitchUrl;
            _timeZone = AppTimeZone.Resolve(timeZoneId);
            _templateService = templateService;
        }

        // UA: Відправити сповіщення залежно від типу контенту.
        //     Повертає ID відправленого повідомлення Telegram як рядок —
        //     підтвердження реальної відправки. На відміну від Discord-вебхука,
        //     Telegram Bot API синхронний: якщо await завершився без винятку
        //     (напр. ApiRequestException при 429 "Too Many Requests"), повідомлення
        //     вже гарантовано доставлене — тут достатньо просто повернути MessageId.
        // EN: Send notification depending on content type.
        //     Returns the sent Telegram message's ID as a string — confirmation
        //     of an actual send. Unlike Discord's webhook, the Telegram Bot API
        //     is synchronous: if the await completes without throwing (e.g. an
        //     ApiRequestException on a 429 "Too Many Requests"), the message is
        //     already guaranteed delivered — just returning MessageId is enough.
        //     note — зворотний виклик з поясненням, якщо повідомлення пішло не
        //     у звичайному вигляді (фото замість прев'ю посилання).
        //     note — a callback with an explanation if the message went out in
        //     a non-standard form (a photo instead of a link preview).
        public async Task<string> SendNotificationAsync(VideoInfo video, string? chatIdOverride = null,
            Action<string>? note = null)
        {
            var templates = _templateService.GetTemplates().Telegram;

            // UA: Вибір шаблону залежно від типу контенту
            // EN: Select template based on content type
            var template = video.Type switch
            {
                VideoType.Live => templates.Live,
                VideoType.Upcoming => templates.Upcoming,
                VideoType.Video => templates.Video,
                VideoType.Short => templates.Short,
                _ => throw new ArgumentOutOfRangeException()
            };

            // UA: ScheduledStartTime передається для підстановки дати у шаблон
            // EN: Pass ScheduledStartTime for date substitution in template
            var text = _templateService.Apply(
                template,
                video.Title,
                video.Url,
                _twitchUrl,
                _timeZone,
                video.ScheduledStartTime);

            var chat = TargetChat(chatIdOverride);
            var hasThumbnail = !string.IsNullOrEmpty(video.VideoId) || !string.IsNullOrEmpty(video.ThumbnailUrl);

            if (_thumbnailAsPhoto && hasThumbnail)
            {
                note?.Invoke("надіслано як фото з мініатюрою (за налаштуванням) / sent as a photo with the thumbnail (per setting)");
                return await SendWithThumbnailAsync(chat, text, video);
            }

            try
            {
                var message = await _botClient.SendMessage(
                    chatId: chat,
                    text: text,
                    parseMode: ParseMode.Html,
                    linkPreviewOptions: new LinkPreviewOptions
                    {
                        IsDisabled = false,
                        Url = video.Url,
                        PreferLargeMedia = true,
                        ShowAboveText = true
                    }
                );

                return message.MessageId.ToString();
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 429 && hasThumbnail)
            {
                // UA: Сервер Telegram не зміг побудувати прев'ю YouTube-посилання
                //     й відповів 429 (серверний збій Telegram,
                //     bugs.telegram.org/c/66775). Повідомлення при цьому НЕ
                //     створене, тож одразу надсилається фото з мініатюрою
                // EN: The Telegram server failed to build the YouTube link
                //     preview and answered 429 (a Telegram server-side failure,
                //     bugs.telegram.org/c/66775). No message
                //     was created, so a photo with the thumbnail is sent right away
                await SafeLogWarningAsync(
                    $"[Telegram] прев'ю посилання недоступне ({ex.Message}) — надсилається мініатюра як фото / link preview unavailable — sending the thumbnail as a photo");
                note?.Invoke("прев'ю посилання недоступне (Telegram 429) — надіслано як фото з мініатюрою / link preview unavailable (Telegram 429) — sent as a photo with the thumbnail");
                return await SendWithThumbnailAsync(chat, text, video);
            }
        }

        // UA: Фото з мініатюрою YouTube + текст. Мініатюра завантажується
        //     самою програмою (не сервером Telegram), тож не залежить від
        //     побудови прев'ю. maxresdefault є не в кожного відео — тоді
        //     береться hqdefault. Текст іде підписом, якщо вміщується в ліміт
        //     підпису; інакше — фото без підпису й окреме текстове
        //     повідомлення без прев'ю.
        // EN: A photo with the YouTube thumbnail + the text. The thumbnail is
        //     downloaded by the app itself (not by the Telegram server), so it
        //     doesn't depend on preview building. Not every video has
        //     maxresdefault — hqdefault is used then. The text goes as the
        //     caption if it fits the caption limit; otherwise a photo without
        //     a caption plus a separate text message without a preview.
        private async Task<string> SendWithThumbnailAsync(ChatId chat, string text, VideoInfo video)
        {
            var image = await DownloadThumbnailAsync(video)
                ?? throw new InvalidOperationException(
                    "Telegram: мініатюру YouTube не вдалось завантажити / failed to download the YouTube thumbnail.");

            using var stream = new MemoryStream(image);
            var fileName = $"{(string.IsNullOrEmpty(video.VideoId) ? "thumbnail" : video.VideoId)}.jpg";

            if (text.Length <= CaptionLimit)
            {
                var photo = await _botClient.SendPhoto(
                    chatId: chat,
                    photo: InputFile.FromStream(stream, fileName),
                    caption: text,
                    parseMode: ParseMode.Html);
                return photo.MessageId.ToString();
            }

            var photoOnly = await _botClient.SendPhoto(chatId: chat, photo: InputFile.FromStream(stream, fileName));
            var textMessage = await _botClient.SendMessage(
                chatId: chat,
                text: text,
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true });
            return $"{photoOnly.MessageId},{textMessage.MessageId}";
        }

        private async Task<byte[]?> DownloadThumbnailAsync(VideoInfo video)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(video.ThumbnailUrl)) candidates.Add(video.ThumbnailUrl);
            if (!string.IsNullOrEmpty(video.VideoId))
                candidates.Add($"https://img.youtube.com/vi/{video.VideoId}/hqdefault.jpg");

            foreach (var url in candidates.Distinct())
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsByteArrayAsync();
                }
                catch (HttpRequestException)
                {
                    // UA: Наступний варіант мініатюри / EN: Try the next thumbnail variant
                }
            }

            return null;
        }

        private async Task SafeLogWarningAsync(string message)
        {
            try { await _log.LogWarningAsync(message); }
            catch { }
        }

        // UA: Повністю ручна відправка — без TemplateService, текст іде як є.
        //     ParseMode.Html лишається увімкненим для HTML-тегів Telegram
        //     (<b>, <i>, <a href>, <code>, <tg-spoiler> тощо) — якщо
        //     тегів немає, звичайний текст
        //     надсилається без змін. Прев'ю посилання лишається на розсуд
        //     Telegram (конкретний URL не задається, на відміну від шаблонного режиму)
        // EN: Fully manual send — bypasses TemplateService, text goes as-is.
        //     ParseMode.Html is kept on for Telegram HTML tags
        //     (<b>, <i>, <a href>, <code>, <tg-spoiler>, etc.) — if no tags
        //     are present, plain text is sent unchanged. Link preview is left
        //     to Telegram's own detection (no specific URL is forced, unlike
        //     in template mode)
        //     imagePaths — необов'язкові локальні файли зображень (ручний
        //     режим). 1 файл іде через sendPhoto з підписом; 2-10 — через
        //     sendMediaGroup, де Telegram вимагає підпис лише на одному
        //     елементі масиву (використовується перший — решта підпису не показують).
        //     null/порожній список — звичайний sendMessage без зображень.
        //     imagePaths — optional local image files (manual mode). 1 file
        //     goes through sendPhoto with a caption; 2-10 go through
        //     sendMediaGroup, where Telegram requires the caption on exactly
        //     one array item (the first one is used — the rest show no caption).
        //     null/empty means a plain sendMessage without images.
        //     Повертає ID(и) надісланого повідомлення (чи кількох — для медіа-
        //     групи) як рядок, або null, якщо не було ні тексту, ні зображень
        //     (відправка взагалі не відбувалась).
        //     Returns the sent message's ID(s) (several, for a media group) as
        //     a string, or null if there was neither text nor images (nothing
        //     was sent at all).
        public async Task<string?> SendRawAsync(string text, IReadOnlyList<string>? imagePaths = null,
            string? chatIdOverride = null)
        {
            var hasImages = imagePaths is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(text) && !hasImages) return null;

            if (!hasImages)
            {
                var sent = await _botClient.SendMessage(
                    chatId: TargetChat(chatIdOverride),
                    text: text,
                    parseMode: ParseMode.Html
                );
                return sent.MessageId.ToString();
            }

            if (imagePaths!.Count == 1)
            {
                await using var stream = File.OpenRead(imagePaths[0]);
                var sent = await _botClient.SendPhoto(
                    chatId: TargetChat(chatIdOverride),
                    photo: InputFile.FromStream(stream, Path.GetFileName(imagePaths[0])),
                    caption: text,
                    parseMode: ParseMode.Html
                );
                return sent.MessageId.ToString();
            }

            // UA: Telegram.Bot 22.x: sendPhoto, sendMediaGroup, InputFile.FromStream,
            //     InputMediaPhoto.Caption/ParseMode
            // EN: Telegram.Bot 22.x: sendPhoto, sendMediaGroup, InputFile.FromStream,
            //     InputMediaPhoto.Caption/ParseMode
            var files = imagePaths.Take(MaxAttachmentsPerMessage).ToList();

            // UA: Потоки відкриваються ОДИН ЗА ОДНИМ усередині try, а не разом
            //     через .Select(File.OpenRead).ToList() до try — інакше, якщо
            //     відкриття, скажімо, третього файлу кине виняток (видалили з
            //     диска між вибором у галереї й відправкою, немає прав
            //     доступу), два вже відкриті дескриптори першого й другого
            //     файлів ніхто не закриє: виняток стався ще ДО входу в
            //     try/finally, що їх звільняє.
            // EN: Streams are opened ONE AT A TIME inside the try, not all at
            //     once via .Select(File.OpenRead).ToList() before the try —
            //     otherwise, if opening, say, the third file throws (removed
            //     from disk between the gallery pick and sending, no access
            //     rights), the two already-open handles for the first and
            //     second files are never released: the exception happens
            //     BEFORE entering the try/finally that disposes them.
            var streams = new List<FileStream>();
            try
            {
                foreach (var file in files)
                    streams.Add(File.OpenRead(file));

                var media = new List<InputMediaPhoto>();
                for (var i = 0; i < files.Count; i++)
                {
                    var item = new InputMediaPhoto(InputFile.FromStream(streams[i], Path.GetFileName(files[i])));
                    if (i == 0)
                    {
                        item.Caption = text;
                        item.ParseMode = ParseMode.Html;
                    }

                    media.Add(item);
                }

                var sent = await _botClient.SendMediaGroup(chatId: TargetChat(chatIdOverride), media: media);
                return string.Join(",", sent.Select(m => m.MessageId));
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        // UA: Telegram дозволяє щонайбільше 10 елементів у медіа-групі — це
        //     жорсткий ліміт самого API, не вибір UI (на відміну від
        //     аналогічної константи в DiscordService)
        // EN: Telegram allows at most 10 items in a media group — this is a
        //     hard API limit, not a UI choice (unlike the equivalent constant
        //     in DiscordService)
        public const int MaxAttachmentsPerMessage = 10;

        // UA: Перевірка з'єднання БЕЗ фактичної відправки — щоб проблему зі
        //     з'єднанням було видно ще до натискання "Надіслати", а не
        //     постфактум. GetMe() підтверджує, що токен бота взагалі
        //     дійсний; GetChat() окремо підтверджує, що саме ЦЕЙ бот бачить
        //     саме ЦЕЙ канал (ловить, наприклад, друкарську помилку в
        //     username каналу — валідний токен тут нічого не гарантує).
        //     Обидва виклики — легкі, лише читають інформацію, нічого не
        //     публікують і не видно жодному підписнику каналу.
        // EN: A connection check WITHOUT an actual send — so a connectivity
        //     problem is visible before "Send" is pressed, not after the
        //     fact. GetMe() confirms the bot token is valid at all; GetChat()
        //     separately confirms THIS bot can see THIS channel (catches,
        //     say, a typo in the channel username — a valid token guarantees
        //     nothing there). Both calls are lightweight, read-only, post
        //     nothing, and are invisible to any channel subscriber.
        public async Task<(bool Ok, string? Error)> CheckConnectionAsync(string? chatIdOverride = null)
        {
            try
            {
                await _botClient.GetMe();
            }
            catch (Exception ex)
            {
                return (false, $"Telegram: {ex.Message}");
            }

            try
            {
                await _botClient.GetChat(TargetChat(chatIdOverride));
            }
            catch (Exception ex)
            {
                return (false, $"Telegram ({DescribeTarget(chatIdOverride)}): {ex.Message}");
            }

            return (true, null);
        }

        // UA: Цільовий чат відправки. chatIdOverride — тестовий чат (числовий
        //     ID особистого чату з ботом); null/порожній — канал із
        //     налаштувань. Числовий рядок перетворюється на числовий ChatId
        //     явно, щоб не залежати від того, як бібліотека трактує рядок
        // EN: The send target chat. chatIdOverride is the test chat (the
        //     numeric ID of the private chat with the bot); null/empty means
        //     the channel from settings. A numeric string is converted into a
        //     numeric ChatId explicitly, so as not to depend on how the
        //     library interprets a string
        private ChatId TargetChat(string? chatIdOverride)
        {
            var target = string.IsNullOrWhiteSpace(chatIdOverride) ? _channelUsername : chatIdOverride.Trim();
            return long.TryParse(target, out var numericId) ? new ChatId(numericId) : new ChatId(target);
        }

        private string DescribeTarget(string? chatIdOverride)
            => string.IsNullOrWhiteSpace(chatIdOverride) ? _channelUsername : chatIdOverride.Trim();

        // UA: Особистий чат, знайдений у вхідних повідомленнях бота
        // EN: A private chat found among the bot's incoming messages
        public sealed record PrivateChatInfo(long Id, string DisplayName);

        // UA: Пошук особистих чатів, з яких боту нещодавно писали (напр.
        //     /start) — щоб отримати власний числовий chat ID без сторонніх
        //     ботів. getUpdates лише читає чергу оновлень (offset не
        //     передається, тож черга не підтверджується й не очищується).
        //     Telegram зберігає оновлення до 24 годин; якщо для бота
        //     встановлено вебхук, getUpdates поверне помилку 409.
        // EN: Finds private chats that recently wrote to the bot (e.g.
        //     /start) — to obtain one's own numeric chat ID without
        //     third-party bots. getUpdates only reads the update queue (no
        //     offset is passed, so the queue isn't acknowledged or cleared).
        //     Telegram keeps updates for up to 24 hours; if a webhook is set
        //     for the bot, getUpdates returns a 409 error.
        public static async Task<IReadOnlyList<PrivateChatInfo>> FindPrivateChatsAsync(string botToken)
        {
            var client = new TelegramBotClient(botToken);
            var updates = await client.GetUpdates(
                limit: 100,
                timeout: 0,
                allowedUpdates: new[] { UpdateType.Message });

            return updates
                .Select(u => u.Message?.Chat)
                .Where(c => c != null && c.Type == ChatType.Private)
                .GroupBy(c => c!.Id)
                .Select(g =>
                {
                    var chat = g.Last()!;
                    var name = string.Join(" ", new[] { chat.FirstName, chat.LastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (!string.IsNullOrWhiteSpace(chat.Username))
                        name = string.IsNullOrEmpty(name) ? $"@{chat.Username}" : $"{name} (@{chat.Username})";
                    return new PrivateChatInfo(chat.Id, name);
                })
                .ToList();
        }
    }
}