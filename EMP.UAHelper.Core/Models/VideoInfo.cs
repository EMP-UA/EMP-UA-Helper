// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Модель що описує відео/трансляцію отриману з YouTube API
// EN: Model describing a video/stream retrieved from YouTube API
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
        // UA: Унікальний ID відео на YouTube
        // EN: Unique YouTube video ID
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

        // UA: Пряме посилання на відео
        // EN: Direct link to the video
        public string Url => $"https://www.youtube.com/watch?v={VideoId}";

        // UA: Посилання на превью відео
        // EN: Video thumbnail URL
        public string ThumbnailUrl => $"https://img.youtube.com/vi/{VideoId}/maxresdefault.jpg";
    }
}