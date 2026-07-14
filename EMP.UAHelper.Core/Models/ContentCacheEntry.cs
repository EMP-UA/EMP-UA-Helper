// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Один запис локального кешу контенту — YouTube-відео/трансляція,
//     побачена програмою хоча б раз, незалежно від того, чи вона й досі
//     потрапляє в останні ~15 записів RSS-фіду
// EN: A single local content cache entry — a YouTube video/stream the app
//     has seen at least once, regardless of whether it's still within the
//     RSS feed's last ~15 entries
using EMP.UAHelper.Core.Models;

namespace EMP.UAHelper.Core.Models
{
    public class ContentCacheEntry
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public VideoType Type { get; set; }
        public long? ScheduledStartTime { get; set; }

        // UA: Реальна дата публікації запису на YouTube (snippet.publishedAt) —
        //     докладніше про призначення поля див. VideoInfo
        // EN: The record's real YouTube publish date (snippet.publishedAt) —
        //     see VideoInfo for a fuller explanation of this field
        public long? PublishedAt { get; set; }

        // UA: Реальний час старту, якщо запис колись був активною трансляцією
        //     (liveStreamingDetails.actualStartTime) — точніший за PublishedAt
        //     для завершених стримів
        // EN: The real start time if the entry was ever a live broadcast
        //     (liveStreamingDetails.actualStartTime) — more accurate than
        //     PublishedAt for finished streams
        public long? ActualStartTime { get; set; }

        // UA: Unix-час, коли програма вперше локально побачила цей запис —
        //     не дата публікації на YouTube, а лише позначка для ротації кешу
        // EN: Unix time when the app first saw this entry locally —
        //     not the YouTube publish date, just a marker for cache rotation
        public long DiscoveredAt { get; set; }

        public string Url => string.IsNullOrEmpty(VideoId)
            ? string.Empty
            : $"https://www.youtube.com/watch?v={VideoId}";

        // UA: Легка версія превью (mqdefault) — для списку вибору, не для
        //     фінального сповіщення (там лишається важча maxresdefault)
        // EN: Lightweight thumbnail (mqdefault) — for the picker list, not
        //     the final notification (which still uses heavier maxresdefault)
        public string ThumbnailUrl => string.IsNullOrEmpty(VideoId)
            ? string.Empty
            : $"https://img.youtube.com/vi/{VideoId}/mqdefault.jpg";
    }
}