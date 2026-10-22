// =============================================================================
// EMP UA Helper — TemplateEditorWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Код вікна редактора шаблонів повідомлень
// EN: Template editor window code-behind
// =============================================================================
using EMP.UAHelper.Core.Models;
using EMP.UAHelper.Core.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace EMP.UAHelper.WPF
{
    public partial class TemplateEditorWindow : Window
    {
        private readonly TemplateService _templateService;
        private readonly LocalizationService _loc;

        // UA: Один і той самий текст може опинитись у звичайному повідомленні
        //     (Telegram) або в окремому полі embed (Discord title/description) —
        //     тому ліміти різні, а не один спільний, як у ручному режимі
        // EN: The same text can end up in a plain message (Telegram) or in a
        //     separate embed field (Discord title/description) — so the limits
        //     differ instead of sharing one value, unlike manual mode
        private const int TelegramTemplateTextLimit = 4096;
        private const int DiscordEmbedTitleLimit = 256;
        private const int DiscordEmbedDescriptionLimit = 4096;

        // UA: Поле, його лічильник, лейбл для повідомлення про перевищення й
        //     застосовний ліміт — одне джерело правди для лічильників і
        //     перевірки перед збереженням
        // EN: A field, its counter, the label for the over-limit message and
        //     the applicable limit — one source of truth for both the live
        //     counters and the pre-save check
        // UA: TextBox — явно System.Windows.Controls, бо проєкт також посилається
        //     на System.Windows.Forms (трей-іконка), де є однойменний тип
        // EN: TextBox is qualified as System.Windows.Controls because the
        //     project also references System.Windows.Forms (tray icon), which
        //     has a type with the same name
        private readonly record struct LimitEntry(System.Windows.Controls.TextBox Box, TextBlock Counter, string Label, int Limit);

        public TemplateEditorWindow(TemplateService templateService, LocalizationService loc)
        {
            InitializeComponent();
            _templateService = templateService;
            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;
            ApplyLocalization();
            LoadTemplates();
            WireCounters();
            TextBoxClipboard.AttachAll(this, _loc, null, message => _ = new CrashLogService().LogWarningAsync(message));
        }

        // UA: Будується щоразу (а не кешується в полі), бо лейбли залежать від
        //     поточної мови — LanguageChanged міг змінити _loc.Get(...) з часу
        //     попередньої побудови
        // EN: Built each time (not cached in a field) because the labels
        //     depend on the current language — LanguageChanged may have
        //     changed _loc.Get(...) since the previous build
        private IEnumerable<LimitEntry> GetLimitEntries()
        {
            var tg = _loc.Get("templates.tab.telegram");
            var dc = _loc.Get("templates.tab.discord");
            var titleLabel = _loc.Get("templates.discord.title_label");
            var bodyLabel = _loc.Get("templates.discord.body_label");

            yield return new LimitEntry(TgLive, TgLiveCounter, $"{tg} — {_loc.Get("type.live")}", TelegramTemplateTextLimit);
            yield return new LimitEntry(TgUpcoming, TgUpcomingCounter, $"{tg} — {_loc.Get("type.upcoming")}", TelegramTemplateTextLimit);
            yield return new LimitEntry(TgVideo, TgVideoCounter, $"{tg} — {_loc.Get("type.video")}", TelegramTemplateTextLimit);
            yield return new LimitEntry(TgShort, TgShortCounter, $"{tg} — {_loc.Get("type.short")}", TelegramTemplateTextLimit);

            yield return new LimitEntry(DcTitleLive, DcTitleLiveCounter, $"{dc} ({titleLabel}) — {_loc.Get("type.live")}", DiscordEmbedTitleLimit);
            yield return new LimitEntry(DcLive, DcLiveCounter, $"{dc} ({bodyLabel}) — {_loc.Get("type.live")}", DiscordEmbedDescriptionLimit);
            yield return new LimitEntry(DcTitleUpcoming, DcTitleUpcomingCounter, $"{dc} ({titleLabel}) — {_loc.Get("type.upcoming")}", DiscordEmbedTitleLimit);
            yield return new LimitEntry(DcUpcoming, DcUpcomingCounter, $"{dc} ({bodyLabel}) — {_loc.Get("type.upcoming")}", DiscordEmbedDescriptionLimit);
            yield return new LimitEntry(DcTitleVideo, DcTitleVideoCounter, $"{dc} ({titleLabel}) — {_loc.Get("type.video")}", DiscordEmbedTitleLimit);
            yield return new LimitEntry(DcVideo, DcVideoCounter, $"{dc} ({bodyLabel}) — {_loc.Get("type.video")}", DiscordEmbedDescriptionLimit);
            yield return new LimitEntry(DcTitleShort, DcTitleShortCounter, $"{dc} ({titleLabel}) — {_loc.Get("type.short")}", DiscordEmbedTitleLimit);
            yield return new LimitEntry(DcShort, DcShortCounter, $"{dc} ({bodyLabel}) — {_loc.Get("type.short")}", DiscordEmbedDescriptionLimit);
        }

        // UA: Підписка на TextChanged — лише один раз, у конструкторі, після
        //     LoadTemplates (щоб перший показ лічильників не вимагав окремого
        //     проходу). Лейбли в GetLimitEntries перерахуються при наступному
        //     викликові, тож нова мова на самих делегатах нічого не ламає
        // EN: Subscribed to TextChanged once, in the constructor, after
        //     LoadTemplates (so the first counter display doesn't need a
        //     separate pass). Labels in GetLimitEntries are recomputed on the
        //     next call, so a language change doesn't break the delegates
        private void WireCounters()
        {
            foreach (var entry in GetLimitEntries())
                entry.Box.TextChanged += (_, _) => SetCharCount(entry.Counter, entry.Box.Text.Length, entry.Limit);

            UpdateAllCounters();
        }

        private void UpdateAllCounters()
        {
            foreach (var entry in GetLimitEntries())
                SetCharCount(entry.Counter, entry.Box.Text.Length, entry.Limit);
        }

        // UA: Червоний при перевищенні — та сама семантика, що й у вікні
        //     ручної відправки: це не м'яке попередження, а ознака того, що
        //     реальна відправка цим шаблоном завершиться помилкою API
        // EN: Red when exceeded — the same semantics as in the manual-send
        //     window: this isn't a soft warning, it's a sign that an actual
        //     send with this template will fail at the API level
        private static void SetCharCount(TextBlock target, int length, int limit)
        {
            target.Text = $"{length} / {limit}";
            target.Foreground = length > limit
                ? new SolidColorBrush(Color.FromRgb(0xE0, 0x60, 0x5A))
                : new SolidColorBrush(Color.FromRgb(0x6A, 0x5A, 0x80));
        }

        // UA: Застосувати локалізацію до всіх елементів
        // EN: Apply localization to all elements
        private void ApplyLocalization()
        {
            Title = _loc.Get("templates.title");
            TxtHeader.Text = _loc.Get("templates.header");
            TxtHint.Text = _loc.Get("templates.hint");
            TabTelegram.Header = _loc.Get("templates.tab.telegram");
            TabDiscord.Header = _loc.Get("templates.tab.discord");

            // UA: Telegram секції / EN: Telegram sections
            TxtTgLive.Text = _loc.Get("templates.live");
            TxtTgUpcoming.Text = _loc.Get("templates.upcoming");
            TxtTgVideo.Text = _loc.Get("templates.video");
            TxtTgShort.Text = _loc.Get("templates.short");

            // UA: Discord секції / EN: Discord sections
            TxtDcLive.Text = _loc.Get("templates.live");
            TxtDcLiveTitleLabel.Text = _loc.Get("templates.discord.title_label");
            TxtDcLiveBodyLabel.Text = _loc.Get("templates.discord.body_label");
            TxtDcUpcoming.Text = _loc.Get("templates.upcoming");
            TxtDcUpcomingTitleLabel.Text = _loc.Get("templates.discord.title_label");
            TxtDcUpcomingBodyLabel.Text = _loc.Get("templates.discord.body_label");
            TxtDcVideo.Text = _loc.Get("templates.video");
            TxtDcVideoTitleLabel.Text = _loc.Get("templates.discord.title_label");
            TxtDcVideoBodyLabel.Text = _loc.Get("templates.discord.body_label");
            TxtDcShort.Text = _loc.Get("templates.short");
            TxtDcShortTitleLabel.Text = _loc.Get("templates.discord.title_label");
            TxtDcShortBodyLabel.Text = _loc.Get("templates.discord.body_label");

            BtnReset.Content = _loc.Get("templates.reset");
            BtnSave.Content = _loc.Get("templates.save");

            // UA: Підсвічується активна мова
            // EN: The active language is highlighted
            var active = new SolidColorBrush(Color.FromRgb(0x8A, 0x46, 0xC1));
            var inactive = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x25));
            BtnUA.Background = _loc.Language == UiLanguage.UA ? active : inactive;
            BtnEN.Background = _loc.Language == UiLanguage.EN ? active : inactive;
        }

        // UA: Перемикач мови / EN: Language switcher
        private void BtnUA_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.UA);

        private void BtnEN_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.EN);

        // UA: Завантажити поточні шаблони в поля вводу
        // EN: Load current templates into input fields
        private void LoadTemplates()
        {
            var t = _templateService.GetTemplates();

            TgLive.Text = t.Telegram.Live;
            TgUpcoming.Text = t.Telegram.Upcoming;
            TgVideo.Text = t.Telegram.Video;
            TgShort.Text = t.Telegram.Short;

            DcTitleLive.Text = t.DiscordTitles.Live;
            DcLive.Text = t.Discord.Live;
            DcTitleUpcoming.Text = t.DiscordTitles.Upcoming;
            DcUpcoming.Text = t.Discord.Upcoming;
            DcTitleVideo.Text = t.DiscordTitles.Video;
            DcVideo.Text = t.Discord.Video;
            DcTitleShort.Text = t.DiscordTitles.Short;
            DcShort.Text = t.Discord.Short;
        }

        // UA: Зберегти зміни / EN: Save changes
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var exceeded = GetLimitEntries()
                .Where(entry => entry.Box.Text.Length > entry.Limit)
                .Select(entry => string.Format(_loc.Get("send.raw.limit.line"), entry.Label, entry.Box.Text.Length, entry.Limit))
                .ToList();

            if (exceeded.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    string.Join("\n", exceeded) + "\n\n" + _loc.Get("templates.limit.blocked"),
                    _loc.Get("app.name"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var updated = new MessageTemplates
            {
                Telegram = new PlatformTemplates
                {
                    Live = TgLive.Text,
                    Upcoming = TgUpcoming.Text,
                    Video = TgVideo.Text,
                    Short = TgShort.Text
                },
                Discord = new PlatformTemplates
                {
                    Live = DcLive.Text,
                    Upcoming = DcUpcoming.Text,
                    Video = DcVideo.Text,
                    Short = DcShort.Text
                },
                DiscordTitles = new PlatformTemplates
                {
                    Live = DcTitleLive.Text,
                    Upcoming = DcTitleUpcoming.Text,
                    Video = DcTitleVideo.Text,
                    Short = DcTitleShort.Text
                }
            };

            await _templateService.SaveAsync(updated);

            System.Windows.MessageBox.Show(
                _loc.Get("templates.saved"),
                _loc.Get("app.name"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // UA: Скинути до дефолтних значень / EN: Reset to default values
        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                _loc.Get("templates.reset.confirm"),
                _loc.Get("app.name"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            var path = Path.Combine(AppContext.BaseDirectory, "templates.json");
            if (File.Exists(path)) File.Delete(path);

            var freshService = new TemplateService();
            await _templateService.SaveAsync(freshService.GetTemplates());

            LoadTemplates();

            System.Windows.MessageBox.Show(
                _loc.Get("templates.reset.done"),
                _loc.Get("app.name"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // UA: Відписка від події при закритті
        // EN: Unsubscribe from event on close
        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}