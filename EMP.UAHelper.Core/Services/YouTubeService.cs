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

        // UA: RSS фід каналу не потребує API ключа і не витрачає квоту
        // EN: Channel RSS feed requires no API key and uses no quota
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

            // UA: Спочатку шукаємо активну або заплановану трансляцію через search.list без eventType
            // EN: First search for active or upcoming stream via search.list without eventType
            var liveResult = await CheckLiveAsync(youtubeService);
            if (liveResult != null) return liveResult;

            // UA: Якщо трансляції немає — беремо останнє відео з RSS (без квоти)
            // EN: If no stream — get latest video from RSS (no quota cost)
            return await GetLatestFromRssAsync();
        }

        // UA: Перевірка активної трансляції через search.list з фільтром по liveBroadcastContent
        // EN: Check for active stream via search.list filtered by liveBroadcastContent
        private async Task<VideoInfo?> CheckLiveAsync(Google.Apis.YouTube.v3.YouTubeService service)
        {
            var request = service.Search.List("snippet");
            request.ChannelId = _channelId;
            request.Type = "video";
            request.MaxResults = 5;

            var response = await request.ExecuteAsync();

            // UA: Шукаємо відео з активною або запланованою трансляцією
            // EN: Look for video with active or upcoming live broadcast
            foreach (var item in response.Items)
            {
                if (item.Snippet.LiveBroadcastContent == "live")
                    return new VideoInfo
                    {
                        VideoId = item.Id.VideoId,
                        Title = item.Snippet.Title,
                        Type = VideoType.Live
                    };

                if (item.Snippet.LiveBroadcastContent == "upcoming")
                    return new VideoInfo
                    {
                        VideoId = item.Id.VideoId,
                        Title = item.Snippet.Title,
                        Type = VideoType.Upcoming
                    };
            }

            return null;
        }

        // UA: Отримати останнє відео або шортс через RSS фід (без витрат квоти)
        // EN: Get latest video or short via RSS feed (no quota cost)
        private async Task<VideoInfo?> GetLatestFromRssAsync()
        {
            using var httpClient = new HttpClient();
            var xml = await httpClient.GetStringAsync(RssUrl);
            var doc = XDocument.Parse(xml);

            XNamespace ns = "http://www.w3.org/2005/Atom";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

            var entry = doc.Descendants(ns + "entry").FirstOrDefault();
            if (entry == null) return null;

            var videoId = entry.Element(yt + "videoId")?.Value ?? string.Empty;
            var title = entry.Element(ns + "title")?.Value ?? string.Empty;

            // UA: Визначаємо чи це шортс через #Shorts в назві
            // EN: Detect if it's a Short by checking #Shorts in title
            bool isShort = title.Contains("#Shorts", StringComparison.OrdinalIgnoreCase)
                        || title.Contains("#Short", StringComparison.OrdinalIgnoreCase);

            return new VideoInfo
            {
                VideoId = videoId,
                Title = title,
                Type = isShort ? VideoType.Short : VideoType.Video
            };
        }
    }
}