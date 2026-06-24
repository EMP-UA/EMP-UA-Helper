// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Код вікна редактора шаблонів повідомлень
// EN: Template editor window code-behind
using EMP.UAHelper.Core.Models;
using EMP.UAHelper.Core.Services;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace EMP.UAHelper.WPF
{
    public partial class TemplateEditorWindow : Window
    {
        private readonly TemplateService _templateService;
        private readonly LocalizationService _loc;

        public TemplateEditorWindow(TemplateService templateService, LocalizationService loc)
        {
            InitializeComponent();
            _templateService = templateService;
            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;
            ApplyLocalization();
            LoadTemplates();
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

            // UA: Підсвічуємо активну мову
            // EN: Highlight active language
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

        // UA: Відписуємось від події при закритті
        // EN: Unsubscribe from event on close
        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}