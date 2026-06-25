// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для отримання інформації про відео/трансляції з YouTube API та RSS
// EN: Service for retrieving video/stream information from YouTube API and RSS
using EMP.UAHelper.Core.Models;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Xml.Linq;

namespace EMP.UAHelper.Core.Services
{
    public class YouTubeService
    {
        private readonly string _apiKey;
        private readonly string _channelId;

        // UA: RSS фід каналу — без квоти, без OAuth
        // EN: Channel RSS feed — no quota, no OAuth
        private string RssUrl => $"https://www.youtube.com/feeds/videos.xml?channel_id={_channelId}";

        public YouTubeService(string apiKey, string channelId)
        {
            _apiKey = apiKey;
            _channelId = channelId;
        }

        // UA: Отримати найактуальніший контент з каналу
        // EN: Get the most recent content from the channel
        public async Task<VideoInfo?> GetLatestContentAsync()
        {
            var youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = _apiKey
            });

            // UA: Крок 1 — отримуємо ID останніх відео з RSS (без квоти)
            // EN: Step 1 — get latest video IDs from RSS (no quota cost)
            var videoIds = await GetVideoIdsFromRssAsync();
            if (videoIds.Count == 0) return null;

            // UA: Крок 2 — один запит videos.list для всіх ID (1 unit квоти)
            // EN: Step 2 — single videos.list request for all IDs (1 quota unit)
            return await GetVideoInfoAsync(youtubeService, videoIds);
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

            // UA: Беремо останні 5 відео щоб охопити можливу трансляцію
            // EN: Take last 5 videos to cover possible stream
            return doc.Descendants(ns + "entry")
                .Take(5)
                .Select(e => e.Element(yt + "videoId")?.Value ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }

        // UA: Отримати інформацію про відео через videos.list
        // EN: Get video information via videos.list
        private async Task<VideoInfo?> GetVideoInfoAsync(
            Google.Apis.YouTube.v3.YouTubeService service,
            List<string> videoIds)
        {
            var request = service.Videos.List("snippet,contentDetails,liveStreamingDetails");
            request.Id = string.Join(",", videoIds);

            var response = await request.ExecuteAsync();
            if (response.Items.Count == 0) return null;

            // UA: Спочатку шукаємо активну трансляцію
            // EN: First look for active live stream
            var live = response.Items.FirstOrDefault(
                v => v.Snippet.LiveBroadcastContent == "live");
            if (live != null)
                return new VideoInfo
                {
                    VideoId = live.Id,
                    Title = live.Snippet.Title,
                    Type = VideoType.Live
                };

            // UA: Потім шукаємо заплановану трансляцію
            // EN: Then look for upcoming stream
            var upcoming = response.Items.FirstOrDefault(
                v => v.Snippet.LiveBroadcastContent == "upcoming");
            if (upcoming != null)
            {
                var scheduledStart = upcoming.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset;
                return new VideoInfo
                {
                    VideoId = upcoming.Id,
                    Title = upcoming.Snippet.Title,
                    Type = VideoType.Upcoming,
                    ScheduledStartTime = scheduledStart.HasValue
                        ? scheduledStart.Value.ToUnixTimeSeconds()
                        : null
                };
            }

            // UA: Якщо трансляцій немає — беремо перше звичайне відео або шортс
            // EN: If no streams — take first regular video or short
            var latest = response.Items.FirstOrDefault(
                v => v.Snippet.LiveBroadcastContent == "none");
            if (latest == null) return null;

            // UA: Визначаємо шортс через duration <= 60 секунд
            // EN: Detect short via duration <= 60 seconds
            var duration = System.Xml.XmlConvert.ToTimeSpan(
                latest.ContentDetails.Duration);
            bool isShort = duration.TotalSeconds <= 60;

            return new VideoInfo
            {
                VideoId = latest.Id,
                Title = latest.Snippet.Title,
                Type = isShort ? VideoType.Short : VideoType.Video
            };
        }
    }
}