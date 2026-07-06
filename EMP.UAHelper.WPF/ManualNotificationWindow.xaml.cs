// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Код вікна ручного сповіщення
// EN: Manual notification window code-behind
using EMP.UAHelper.Core.Models;
using EMP.UAHelper.Core.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace EMP.UAHelper.WPF
{
    public partial class ManualNotificationWindow : Window
    {
        private readonly NotificationDispatcher _dispatcher;
        private readonly LocalizationService _loc;

        public ManualNotificationWindow(NotificationDispatcher dispatcher, LocalizationService loc)
        {
            InitializeComponent();
            _dispatcher = dispatcher;
            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;
            ApplyLocalization();
            TypeCombo.SelectedIndex = 0;
        }

        // UA: Застосувати локалізацію до всіх елементів
        // EN: Apply localization to all elements
        private void ApplyLocalization()
        {
            Title = _loc.Get("manual.title");
            TxtHeader.Text = _loc.Get("manual.header");
            TxtDescription.Text = _loc.Get("manual.description");
            TxtTitleLabel.Text = _loc.Get("manual.title_label");
            TxtTypeLabel.Text = _loc.Get("manual.type_label");
            TxtUrlLabel.Text = _loc.Get("manual.url_label");
            TxtDateLabel.Text = _loc.Get("manual.date_label");
            TxtTimeLabel.Text = _loc.Get("manual.time_label");
            TxtTypeHint.Text = _loc.Get("manual.type_hint");
            TxtUrlHint.Text = _loc.Get("manual.url_hint");
            BtnSend.Content = _loc.Get("manual.send");

            ItemLive.Content = _loc.Get("type.live");
            ItemUpcoming.Content = _loc.Get("type.upcoming");
            ItemVideo.Content = _loc.Get("type.video");
            ItemShort.Content = _loc.Get("type.short");

            // UA: Підсвічуємо активну мову
            // EN: Highlight active language
            var active = new SolidColorBrush(Color.FromRgb(0x8A, 0x46, 0xC1));
            var inactive = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x25));
            BtnUA.Background = _loc.Language == UiLanguage.UA ? active : inactive;
            BtnEN.Background = _loc.Language == UiLanguage.EN ? active : inactive;
        }

        // UA: Перемикач мови
        // EN: Language switcher
        private void BtnUA_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.UA);

        private void BtnEN_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.EN);

        // UA: Панель дати/часу видима лише для типу Upcoming
        // EN: Date/time panel is visible only for the Upcoming type
        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelScheduled == null || TypeCombo.SelectedItem is not ComboBoxItem item) return;
            var type = Enum.Parse<VideoType>((string)item.Tag);
            PanelScheduled.Visibility = type == VideoType.Upcoming
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // UA: Конвертувати введені дату/час (Київ) у Unix timestamp
        // EN: Convert the entered date/time (Kyiv) to a Unix timestamp
        private static long? ParseScheduledKyivTime(string dateText, string timeText)
        {
            if (!DateTime.TryParseExact(dateText.Trim(), "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var datePart))
                return null;

            if (!TimeSpan.TryParseExact(timeText.Trim(), @"hh\:mm",
                    CultureInfo.InvariantCulture, out var timePart))
                return null;

            var kyivZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            var localDateTime = datePart.Date + timePart;
            var offset = kyivZone.GetUtcOffset(localDateTime);
            return new DateTimeOffset(localDateTime, offset).ToUnixTimeSeconds();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("manual.validation.title"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var type = Enum.Parse<VideoType>((string)((ComboBoxItem)TypeCombo.SelectedItem).Tag);
            long? scheduled = null;

            if (type == VideoType.Upcoming)
            {
                scheduled = ParseScheduledKyivTime(DateInput.Text, TimeInput.Text);
                if (scheduled == null)
                {
                    System.Windows.MessageBox.Show(
                        _loc.Get("manual.validation.datetime"), _loc.Get("app.name"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var video = new VideoInfo
            {
                Title = title,
                Type = type,
                ScheduledStartTime = scheduled,
                UrlOverride = string.IsNullOrWhiteSpace(UrlInput.Text) ? null : UrlInput.Text.Trim()
            };

            BtnSend.IsEnabled = false;
            try
            {
                await _dispatcher.SendAsync(video);
                System.Windows.MessageBox.Show(
                    _loc.Get("manual.sent"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message, _loc.Get("app.error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSend.IsEnabled = true;
            }
        }

        // UA: Відписуємось від події при закритті
        // EN: Unsubscribe from the event on close
        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}