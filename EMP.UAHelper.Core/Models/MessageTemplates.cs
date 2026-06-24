// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Моделі шаблонів повідомлень для Telegram та Discord
// EN: Message template models for Telegram and Discord
namespace EMP.UAHelper.Core.Models
{
    public class PlatformTemplates
    {
        // UA: Шаблон для активної трансляції
        // EN: Template for active live stream
        public string Live { get; set; } = string.Empty;

        // UA: Шаблон для запланованої трансляції
        // EN: Template for upcoming scheduled stream
        public string Upcoming { get; set; } = string.Empty;

        // UA: Шаблон для звичайного відео
        // EN: Template for regular video
        public string Video { get; set; } = string.Empty;

        // UA: Шаблон для шортсу
        // EN: Template for short video
        public string Short { get; set; } = string.Empty;
    }

    public class MessageTemplates
    {
        // UA: Шаблони для Telegram
        // EN: Telegram templates
        public PlatformTemplates Telegram { get; set; } = new();

        // UA: Шаблони для Discord
        // EN: Discord templates
        public PlatformTemplates Discord { get; set; } = new();

        // UA: Заголовки embed для Discord
        // EN: Discord embed titles
        public PlatformTemplates DiscordTitles { get; set; } = new();
    }
}