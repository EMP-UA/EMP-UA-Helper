// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для отримання інформації про відео/трансляції з YouTube API та RSS
// EN: Service for retrieving video/stream information from YouTube API and RSS
using EMP.UAHelper.Core.Models;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Linq;
using System.Xml.Linq;

namespace EMP.UAHelper.Core.Services
{
    public class YouTubeService
    {
        private readonly string _apiKey;
        private readonly string _channelId;
        private readonly ContentCacheService _cache = new();

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
        public async Task<List<ContentCacheEntry>> GetCandidatesAsync()
        {
            var fetched = await FetchFromYouTubeAsync();
            _cache.Merge(fetched.Select(ToCacheEntry));
            return _cache.GetAll();
        }

        private async Task<List<VideoInfo>> FetchFromYouTubeAsync()
        {
            var youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = _apiKey
            });

            var videoIds = await GetVideoIdsFromRssAsync();
            if (videoIds.Count == 0) return new List<VideoInfo>();

            return await GetVideoInfoListAsync(youtubeService, videoIds);
        }

        // UA: Отримати список ID відео з RSS фіду
        // EN: Get list of video IDs from RSS feed
        private async Task<List<string>> GetVideoIdsFromRssAsync()
        {
            using var httpClient = new HttpClient();
            var xml = await httpClient.GetStringAsync(RssUrl);
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