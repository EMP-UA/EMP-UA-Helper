// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс локального кешу YouTube-контенту. Заплановані трансляції
//     (Upcoming) з майбутньою датою зберігаються без обмеження за кількістю —
//     доки не настане їхня дата. Решта записів ротується за лімітом.
// EN: Local YouTube content cache service. Future-dated Upcoming entries are
//     kept with no count limit — until their date arrives. Everything else
//     is rotated by a fixed limit.
using EMP.UAHelper.Core.Models;
using System.Linq;
using System.Text.Json;

namespace EMP.UAHelper.Core.Services
{
    public class ContentCacheService
    {
        private readonly string _cachePath;

        // UA: Скільки НЕ-Upcoming (або вже минулих Upcoming) записів тримати —
        //     майбутніх Upcoming цей ліміт не стосується
        // EN: How many non-Upcoming (or already-past Upcoming) entries to keep —
        //     future-dated Upcoming entries are exempt from this limit
        private const int MaxOtherEntries = 30;

        public ContentCacheService()
        {
            _cachePath = Path.Combine(AppContext.BaseDirectory, "content-cache.json");
        }

        // UA: Повний вміст кешу — майбутні заплановані трансляції першими
        //     (за зростанням дати), решта — за часом виявлення (найновіші спершу)
        // EN: Full cache contents — future scheduled streams first (ascending
        //     by date), the rest ordered by discovery time (newest first)
        public List<ContentCacheEntry> GetAll()
        {
            var all = Load();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var upcoming = all
                .Where(e => e.Type == VideoType.Upcoming && (e.ScheduledStartTime == null || e.ScheduledStartTime > now))
                .OrderBy(e => e.ScheduledStartTime ?? long.MaxValue);

            var others = all
                .Except(upcoming)
                .OrderByDescending(e => e.DiscoveredAt);

            return upcoming.Concat(others).ToList();
        }

        // UA: Додати/оновити записи зі свіжого опитування YouTube і прибрати
        //     застарілі за лімітом (крім захищених майбутніх Upcoming)
        // EN: Add/update entries from a fresh YouTube check and prune stale
        //     ones over the limit (except protected future Upcoming entries)
        public void Merge(IEnumerable<ContentCacheEntry> fresh)
        {
            var existing = Load().ToDictionary(e => e.VideoId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            foreach (var entry in fresh)
            {
                if (string.IsNullOrEmpty(entry.VideoId)) continue;

                if (existing.TryGetValue(entry.VideoId, out var current))
                {
                    // UA: Дані могли змінитись — оновлюємо, зберігаючи оригінальний
                    //     час першого локального виявлення
                    // EN: Data might have changed — update it, keeping the
                    //     original first-seen timestamp
                    current.Title = entry.Title;
                    current.Type = entry.Type;
                    current.ScheduledStartTime = entry.ScheduledStartTime;
                    current.PublishedAt = entry.PublishedAt;
                    current.ActualStartTime = entry.ActualStartTime;
                }
                else
                {
                    entry.DiscoveredAt = now;
                    existing[entry.VideoId] = entry;
                }
            }

            var protectedUpcoming = existing.Values
                .Where(e => e.Type == VideoType.Upcoming && (e.ScheduledStartTime == null || e.ScheduledStartTime > now))
                .ToList();

            var others = existing.Values
                .Except(protectedUpcoming)
                .OrderByDescending(e => e.DiscoveredAt)
                .Take(MaxOtherEntries);

            Save(protectedUpcoming.Concat(others).ToList());
        }

        private List<ContentCacheEntry> Load()
        {
            if (!File.Exists(_cachePath)) return new List<ContentCacheEntry>();

            try
            {
                var json = File.ReadAllText(_cachePath);
                return JsonSerializer.Deserialize<List<ContentCacheEntry>>(json) ?? new List<ContentCacheEntry>();
            }
            catch
            {
                // UA: Пошкоджений/нечитабельний кеш — не критично, просто починаємо заново
                // EN: Corrupted/unreadable cache — not critical, just start fresh
                return new List<ContentCacheEntry>();
            }
        }

        private void Save(List<ContentCacheEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cachePath, json);
        }
    }
}