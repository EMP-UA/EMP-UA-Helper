// =============================================================================
// EMP UA Helper — YouTubeService.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Сервіс для отримання інформації про відео/трансляції з YouTube API та RSS
// EN: Service for retrieving video/stream information from YouTube API and RSS
// =============================================================================
using EMP.UAHelper.Core.Models;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Linq;
// UA: Потрібен для HttpStatusCode — на відміну від System.Net.Http, простір
//     імен System.Net не входить до неявних using-ів .NET SDK
// EN: Needed for HttpStatusCode — unlike System.Net.Http, the System.Net
//     namespace is not part of the .NET SDK's implicit usings
using System.Net;
using System.Xml.Linq;

namespace EMP.UAHelper.Core.Services
{
    public class YouTubeService
    {
        private readonly string _apiKey;
        private readonly string _channelId;
        private readonly ContentCacheService _cache = new();

        // UA: Один спільний HttpClient на весь час роботи програми. Створювати
        //     new HttpClient() на кожен запит — класична пастка: кожен
        //     екземпляр тримає власне TCP-з'єднання, яке після Dispose ще
        //     хвилини висить у стані TIME_WAIT, і при частих запитах порти
        //     вичерпуються. Явний User-Agent — бо на запити зовсім без нього
        //     YouTube іноді відповідає помилкою замість фіду.
        // EN: A single shared HttpClient for the app's lifetime. Creating a
        //     new HttpClient() per request is the classic trap: each instance
        //     holds its own TCP connection which lingers in TIME_WAIT for
        //     minutes after Dispose, exhausting ports under frequent requests.
        //     An explicit User-Agent is set because YouTube sometimes answers
        //     requests that carry none with an error instead of the feed.
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EMP-UA-Helper/1.3.0 (+https://github.com/EMP-UA/EMP-UA-Helper)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/atom+xml, application/xml;q=0.9, */*;q=0.8");
            return client;
        }

        // UA: RSS фід каналу — без квоти, без OAuth
        // EN: Channel RSS feed — no quota, no OAuth
        private string RssUrl => $"https://www.youtube.com/feeds/videos.xml?channel_id={_channelId}";

        public YouTubeService(string apiKey, string channelId)
        {
            _apiKey = apiKey;
            _channelId = channelId;
        }

        // UA: Автоматичний вибір — той самий пріоритет, що й раніше:
        //     активна трансляція > запланована > останнє звичайне відео/шортс.
        //     Кожен виклик також оновлює локальний кеш кандидатів.
        // EN: Automatic pick — same priority as before: live stream > upcoming >
        //     latest regular video/short. Every call also refreshes the local
        //     candidate cache.
        public async Task<VideoInfo?> GetLatestContentAsync()
        {
            var fetched = await FetchFromYouTubeAsync();
            if (fetched.Count == 0) return null;

            _cache.Merge(fetched.Select(ToCacheEntry));

            var live = fetched.FirstOrDefault(v => v.Type == VideoType.Live);
            if (live != null) return live;

            var upcoming = fetched.FirstOrDefault(v => v.Type == VideoType.Upcoming);
            if (upcoming != null) return upcoming;

            return fetched.FirstOrDefault(v => v.Type == VideoType.Video || v.Type == VideoType.Short);
        }

        // UA: Повний пул кандидатів для ручного вибору в UI — свіжо отримані
        //     з YouTube записи ТА все, що раніше осіло в локальному кеші
        //     (зокрема заплановані трансляції, які вже випали з вікна останніх
        //     записів RSS, але дата яких ще не настала)
        // EN: Full candidate pool for manual UI selection — freshly fetched
        //     YouTube entries AND everything previously cached locally
        //     (including scheduled streams that already fell out of the RSS
        //     recency window, but whose date hasn't arrived yet)
        //     Якщо мережевий запит не вдався (немає інтернету, вичерпано добову
        //     квоту YouTube Data API, тимчасовий збій RSS) — це НЕ причина
        //     показувати порожній список: локальний кеш лишається валідним
        //     джерелом і повертається як є. Виняток прокидається назовні через
        //     out-параметр, щоб UI міг повідомити "дані з кешу, не оновлено",
        //     а не мовчки вдавати, що все гаразд.
        //     If the network request fails (no internet, YouTube Data API daily
        //     quota exhausted, a transient RSS glitch) that's NOT a reason to
        //     show an empty list: the local cache is still a valid source and is
        //     returned as-is. The exception is surfaced via an out parameter so
        //     the UI can report "showing cached data, not refreshed" instead of
        //     silently pretending everything is fine.
        public async Task<List<ContentCacheEntry>> GetCandidatesAsync()
        {
            var (candidates, _) = await GetCandidatesWithStatusAsync();
            return candidates;
        }

        // UA: Та сама вибірка, але з інформацією про те, чи вдалося оновитись
        //     онлайн. fetchError == null означає "список свіжий".
        // EN: Same selection, but with information about whether the online
        //     refresh succeeded. fetchError == null means "the list is fresh".
        public async Task<(List<ContentCacheEntry> Candidates, Exception? FetchError)> GetCandidatesWithStatusAsync()
        {
            try
            {
                var fetched = await FetchFromYouTubeAsync();
                _cache.Merge(fetched.Select(ToCacheEntry));
                return (_cache.GetAll(), null);
            }
            catch (Exception ex)
            {
                return (_cache.GetAll(), ex);
            }
        }

        // =====================================================================
        // UA: Запасний шлях, коли RSS-фід недоступний (YouTube періодично
        //     віддає на нього 500). Свідомо НЕ автоматичний і НЕ типовий:
        //     викликається лише коли користувач сам натисне кнопку в банері
        //     помилки, бо на відміну від безкоштовного RSS він витрачає добову
        //     квоту YouTube Data API — приблизно ApiFallbackQuotaCost одиниць
        //     з типових 10 000 на добу.
        //
        //     Чому саме три запити, а не один дешевий:
        //     • playlistItems (1 юніт) — плейлист завантажень каналу: звичайні
        //       відео та Shorts. Заплановані трансляції в нього, за
        //       повідомленнями розробників, потрапляють ненадійно, тому самого
        //       його недостатньо.
        //     • search.list eventType=upcoming (100 юнітів) — саме заплановані
        //       трансляції. Для цієї програми це найважливіший тип: тільки в
        //       нього є час початку, заради якого існують позначки часу.
        //     • search.list eventType=live (100 юнітів) — активна трансляція.
        //       Пропустити її для програми анонсів стрімів було б найгірше.
        //     Деталі всіх зібраних ID добираються одним videos.list (1 юніт).
        //
        // EN: Fallback path for when the RSS feed is unavailable (YouTube
        //     intermittently answers it with a 500). Deliberately NOT automatic
        //     and NOT the default: it only runs when the user explicitly clicks
        //     the button in the error banner, because unlike the free RSS feed
        //     it consumes the YouTube Data API daily quota — roughly
        //     ApiFallbackQuotaCost units out of the usual 10,000 per day.
        //
        //     Why three requests instead of one cheap call:
        //     • playlistItems (1 unit) — the channel's uploads playlist: regular
        //       videos and Shorts. Scheduled streams reportedly land there
        //       unreliably, so this alone isn't enough.
        //     • search.list eventType=upcoming (100 units) — scheduled streams
        //       specifically. For this app that's the most important type: it's
        //       the only one with a start time, which the timestamps exist for.
        //     • search.list eventType=live (100 units) — the active stream.
        //       Missing it in a stream-announcement app would be the worst case.
        //     Details for all collected IDs are fetched with one videos.list (1 unit).
        // =====================================================================
        public const int ApiFallbackQuotaCost = 202;

        public async Task<(List<ContentCacheEntry> Candidates, Exception? FetchError)> GetCandidatesViaApiAsync()
        {
            try
            {
                var api = CreateApiClient();

                var ids = new List<string>();
                ids.AddRange(await GetUploadsPlaylistIdsAsync(api));
                ids.AddRange(await SearchVideoIdsAsync(api, "upcoming"));
                ids.AddRange(await SearchVideoIdsAsync(api, "live"));

                // UA: videos.list приймає максимум 50 ID за виклик
                // EN: videos.list accepts at most 50 IDs per call
                var distinct = ids.Distinct().Take(50).ToList();

                if (distinct.Count > 0)
                {
                    var infos = await GetVideoInfoListAsync(api, distinct);
                    _cache.Merge(infos.Select(ToCacheEntry));
                }

                return (_cache.GetAll(), null);
            }
            catch (Exception ex)
            {
                return (_cache.GetAll(), ex);
            }
        }

        // UA: ID плейлиста завантажень беремо з channels.list, а не збираємо
        //     самотужки підміною "UC"→"UU": підміна працює для більшості
        //     каналів, але це недокументована домовленість, а не гарантія, і
        //     коштує запит рівно 1 юніт — не та економія, заради якої варто
        //     покладатись на здогад про формат ID
        // EN: The uploads playlist ID comes from channels.list rather than being
        //     assembled by hand via a "UC"→"UU" swap: the swap works for most
        //     channels, but it's an undocumented convention, not a guarantee,
        //     and the request costs exactly 1 unit — not the kind of saving
        //     worth relying on a guess about an ID format for
        private async Task<List<string>> GetUploadsPlaylistIdsAsync(
            Google.Apis.YouTube.v3.YouTubeService api)
        {
            var channelRequest = api.Channels.List("contentDetails");
            channelRequest.Id = _channelId;
            var channelResponse = await channelRequest.ExecuteAsync();

            var uploadsId = channelResponse.Items?
                .FirstOrDefault()?.ContentDetails?.RelatedPlaylists?.Uploads;

            if (string.IsNullOrEmpty(uploadsId)) return new List<string>();

            var itemsRequest = api.PlaylistItems.List("contentDetails");
            itemsRequest.PlaylistId = uploadsId;
            itemsRequest.MaxResults = 20;
            var itemsResponse = await itemsRequest.ExecuteAsync();

            return itemsResponse.Items?
                .Select(i => i.ContentDetails?.VideoId ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? new List<string>();
        }

        // UA: eventType — "upcoming" (заплановані) або "live" (активні).
        //     part=id, бо повні деталі однаково добираються через videos.list —
        //     запитувати тут snippet означало б платити за ті самі дані двічі
        // EN: eventType — "upcoming" (scheduled) or "live" (active).
        //     part=id, because full details are fetched via videos.list anyway —
        //     requesting snippet here would mean paying for the same data twice
        private async Task<List<string>> SearchVideoIdsAsync(
            Google.Apis.YouTube.v3.YouTubeService api, string eventType)
        {
            var request = api.Search.List("id");
            request.ChannelId = _channelId;
            request.Type = "video";
            request.EventType = eventType == "live"
                ? SearchResource.ListRequest.EventTypeEnum.Live
                : SearchResource.ListRequest.EventTypeEnum.Upcoming;
            request.MaxResults = 10;

            var response = await request.ExecuteAsync();

            return response.Items?
                .Select(i => i.Id?.VideoId ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? new List<string>();
        }

        private Google.Apis.YouTube.v3.YouTubeService CreateApiClient() =>
            new(new BaseClientService.Initializer { ApiKey = _apiKey });

        private async Task<List<VideoInfo>> FetchFromYouTubeAsync()
        {
            var youtubeService = CreateApiClient();

            var videoIds = await GetVideoIdsFromRssAsync();
            if (videoIds.Count == 0) return new List<VideoInfo>();

            return await GetVideoInfoListAsync(youtubeService, videoIds);
        }

        // UA: Отримати список ID відео з RSS фіду.
        //     Запит іде через спільний HttpClient з повторами: фід YouTube
        //     періодично віддає 5xx на цілком валідний channel_id, і одна така
        //     секундна відмова не має знеструмлювати все вікно надсилання.
        // EN: Get the list of video IDs from the RSS feed.
        //     The request goes through the shared HttpClient with retries: the
        //     YouTube feed intermittently returns 5xx for a perfectly valid
        //     channel_id, and one such momentary blip shouldn't take down the
        //     whole send window.
        private async Task<List<string>> GetVideoIdsFromRssAsync()
        {
            var xml = await GetRssWithRetryAsync();
            var doc = XDocument.Parse(xml);

            XNamespace ns = "http://www.w3.org/2005/Atom";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

            // UA: YouTube сам обмежує цей фід ~15 записами незалежно від Take() —
            //     запитуємо з невеликим запасом, щоб точно нічого не відкинути
            // EN: YouTube itself caps this feed at ~15 entries regardless of
            //     Take() — request slightly more just to be safe
            return doc.Descendants(ns + "entry")
                .Take(20)
                .Select(e => e.Element(yt + "videoId")?.Value ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }

        // UA: Кілька спроб отримати RSS. Повторюємо лише те, що має шанс
        //     минутись саме себе: 5xx (проблема на боці YouTube), 429 (нас
        //     тимчасово пригальмували) і мережеві збої/таймаути. 4xx на кшталт
        //     404 не повторюємо — це означає неправильний ChannelId у
        //     налаштуваннях, і десять однакових запитів його не виправлять.
        //     Пауза між спробами зростає (0.5с, 1с) — щоб не бити по сервісу,
        //     який і так відповідає помилкою.
        // EN: A few attempts to fetch the RSS. Only retry what has a chance of
        //     resolving on its own: 5xx (a problem on YouTube's side), 429 (we
        //     got throttled) and network failures/timeouts. 4xx such as 404 is
        //     not retried — that means a wrong ChannelId in the settings, and
        //     ten identical requests won't fix it. The pause between attempts
        //     grows (0.5s, 1s) so we don't hammer a service that's already
        //     answering with an error.
        private async Task<string> GetRssWithRetryAsync()
        {
            const int maxAttempts = 3;
            Exception? last = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var response = await Http.GetAsync(RssUrl);

                    var retryable = (int)response.StatusCode >= 500
                                    || response.StatusCode == HttpStatusCode.TooManyRequests;

                    if (!retryable)
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }

                    last = new HttpRequestException(
                        $"RSS feed returned {(int)response.StatusCode} ({response.StatusCode}) " +
                        $"on attempt {attempt}/{maxAttempts}.");
                }
                catch (HttpRequestException ex) { last = ex; }
                catch (TaskCanceledException ex) { last = ex; }

                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }

            throw last ?? new HttpRequestException("RSS feed request failed.");
        }

        // UA: Отримати деталі ВСІХ переданих ID одним запитом
        //     (до 50 ID = 1 квота-юніт незалежно від їхньої кількості)
        // EN: Get details for ALL passed IDs in a single request
        //     (up to 50 IDs = 1 quota unit regardless of count)
        private async Task<List<VideoInfo>> GetVideoInfoListAsync(
            Google.Apis.YouTube.v3.YouTubeService service,
            List<string> videoIds)
        {
            var request = service.Videos.List("snippet,contentDetails,liveStreamingDetails");
            request.Id = string.Join(",", videoIds);

            var response = await request.ExecuteAsync();
            var result = new List<VideoInfo>();

            foreach (var item in response.Items)
            {
                // UA: Ці два поля спільні для будь-якого типу контенту —
                //     обчислюємо один раз, а не в кожній гілці нижче
                // EN: These two fields are common to any content type —
                //     compute once instead of per-branch below
                var publishedAt = item.Snippet.PublishedAtDateTimeOffset;
                var actualStart = item.LiveStreamingDetails?.ActualStartTimeDateTimeOffset;

                if (item.Snippet.LiveBroadcastContent == "live")
                {
                    result.Add(new VideoInfo
                    {
                        VideoId = item.Id,
                        Title = item.Snippet.Title,
                        Type = VideoType.Live,
                        PublishedAt = publishedAt?.ToUnixTimeSeconds(),
                        ActualStartTime = actualStart?.ToUnixTimeSeconds()
                    });
                    continue;
                }

                if (item.Snippet.LiveBroadcastContent == "upcoming")
                {
                    var scheduledStart = item.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset;
                    result.Add(new VideoInfo
                    {
                        VideoId = item.Id,
                        Title = item.Snippet.Title,
                        Type = VideoType.Upcoming,
                        ScheduledStartTime = scheduledStart?.ToUnixTimeSeconds(),
                        PublishedAt = publishedAt?.ToUnixTimeSeconds(),
                        ActualStartTime = actualStart?.ToUnixTimeSeconds()
                    });
                    continue;
                }

                if (item.Snippet.LiveBroadcastContent == "none")
                {
                    var duration = System.Xml.XmlConvert.ToTimeSpan(item.ContentDetails.Duration);
                    bool isShort = duration.TotalSeconds <= 60;

                    result.Add(new VideoInfo
                    {
                        VideoId = item.Id,
                        Title = item.Snippet.Title,
                        Type = isShort ? VideoType.Short : VideoType.Video,
                        PublishedAt = publishedAt?.ToUnixTimeSeconds(),
                        ActualStartTime = actualStart?.ToUnixTimeSeconds()
                    });
                }
            }

            return result;
        }

        private static ContentCacheEntry ToCacheEntry(VideoInfo v) => new()
        {
            VideoId = v.VideoId,
            Title = v.Title,
            Type = v.Type,
            ScheduledStartTime = v.ScheduledStartTime,
            PublishedAt = v.PublishedAt,
            ActualStartTime = v.ActualStartTime
        };
    }
}