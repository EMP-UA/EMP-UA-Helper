// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Модель що описує відео/трансляцію — з YouTube API або введену вручну
// EN: Model describing a video/stream — from the YouTube API or entered manually
namespace EMP.UAHelper.Core.Models
{
    // UA: Тип контенту на каналі
    // EN: Type of content on the channel
    public enum VideoType
    {
        // UA: Активна трансляція
        // EN: Active live stream
        Live,

        // UA: Запланована трансляція
        // EN: Upcoming scheduled stream
        Upcoming,

        // UA: Звичайне відео
        // EN: Regular video
        Video,

        // UA: Короткое відео (Shorts)
        // EN: Short-form video (Shorts)
        Short
    }

    public class VideoInfo
    {
        // UA: Унікальний ID відео на YouTube (порожній для ручних сповіщень без YouTube)
        // EN: Unique YouTube video ID (empty for manual notifications without YouTube)
        public string VideoId { get; set; } = string.Empty;

        // UA: Назва відео або трансляції
        // EN: Title of the video or stream
        public string Title { get; set; } = string.Empty;

        // UA: Тип контенту
        // EN: Content type
        public VideoType Type { get; set; }

        // UA: Запланований час початку трансляції (Unix timestamp для Discord)
        // EN: Scheduled stream start time (Unix timestamp for Discord)
        public long? ScheduledStartTime { get; set; }

        // UA: Явний URL — використовується для ручних сповіщень або джерел, відмінних від YouTube
        // EN: Explicit URL — used for manual notifications or non-YouTube sources
        public string? UrlOverride { get; set; }

        // UA: Явний URL превью — так само для ручних сповіщень
        // EN: Explicit thumbnail URL — same for manual notifications
        public string? ThumbnailOverride { get; set; }

        // UA: Пряме посилання на відео — явний URL має пріоритет над YouTube ID
        // EN: Direct link to the video — explicit URL takes priority over the YouTube ID
        public string Url => !string.IsNullOrEmpty(UrlOverride)
            ? UrlOverride
            : (string.IsNullOrEmpty(VideoId) ? string.Empty : $"https://www.youtube.com/watch?v={VideoId}");

        // UA: Посилання на превью відео
        // EN: Video thumbnail URL
        public string ThumbnailUrl => !string.IsNullOrEmpty(ThumbnailOverride)
            ? ThumbnailOverride
            : (string.IsNullOrEmpty(VideoId) ? string.Empty : $"https://img.youtube.com/vi/{VideoId}/maxresdefault.jpg");
    }
}