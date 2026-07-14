// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Код вікна надсилання сповіщення — приклади для обох платформ,
//     мініатюра, м'яке попередження про нерозпізнане посилання
// EN: Send-notification window code-behind — previews for both platforms,
//     thumbnail, soft warning for unrecognized links
using EMP.UAHelper.Core.Models;
using EMP.UAHelper.Core.Services;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace EMP.UAHelper.WPF
{
    public partial class SendNotificationWindow : Window
    {
        private readonly YouTubeService? _youTubeService;
        private readonly NotificationDispatcher _dispatcher;
        private readonly TemplateService _templateService;
        private readonly AppSettings _settings;
        private readonly LocalizationService _loc;

        private List<ContentCacheEntry> _candidates = new();
        private bool _isReady;

        private class CandidateItem
        {
            public ContentCacheEntry Source { get; init; } = null!;
            public BitmapImage? Thumbnail { get; init; }
            public string Title => Source.Title;
            public string SubLabel { get; init; } = string.Empty;
        }

        public SendNotificationWindow(
            YouTubeService? youTubeService,
            NotificationDispatcher dispatcher,
            TemplateService templateService,
            AppSettings settings,
            LocalizationService loc)
        {
            InitializeComponent();
            _youTubeService = youTubeService;
            _dispatcher = dispatcher;
            _templateService = templateService;
            _settings = settings;
            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;

            ApplyLocalization();

            // UA: Показуємо блоки прикладу лише для реально увімкнених платформ —
            //     стан платформ не змінюється, поки це вікно відкрите, тож
            //     достатньо встановити один раз тут
            // EN: Show preview blocks only for actually enabled platforms —
            //     platform state doesn't change while this window is open,
            //     so setting it once here is enough
            PanelPreviewTelegram.Visibility = _settings.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
            PanelPreviewDiscord.Visibility = _settings.UseDiscord ? Visibility.Visible : Visibility.Collapsed;
            TxtPreviewEmpty.Visibility = (!_settings.UseTelegram && !_settings.UseDiscord)
                ? Visibility.Visible : Visibility.Collapsed;

            TypeCombo.SelectedIndex = 0;

            if (_youTubeService != null)
            {
                ChkAutoLatest.Visibility = Visibility.Visible;
                PanelPickerRow.Visibility = Visibility.Collapsed;
                ChkAutoLatest.IsChecked = true;
                _isReady = true;
                _ = LoadCandidatesAsync();
            }
            else
            {
                _isReady = true;
                UpdatePreview();
            }
        }

        private void ApplyLocalization()
        {
            Title = _loc.Get("send.title");
            TxtHeader.Text = _loc.Get("send.header");
            TxtDescription.Text = _loc.Get("send.description");
            ChkAutoLatest.Content = _loc.Get("send.auto_checkbox");
            TxtPickerHint.Text = _loc.Get("send.picker.hint");
            TxtTitleLabel.Text = _loc.Get("send.title_label");
            TxtTypeLabel.Text = _loc.Get("send.type_label");
            TxtTypeHint.Text = _loc.Get("send.type_hint");
            TxtUrlLabel.Text = _loc.Get("send.url_label");
            TxtUrlHint.Text = _loc.Get("send.url_hint");
            TxtUrlWarning.Text = _loc.Get("send.url.warning");
            TxtPreviewTelegramLabel.Text = _loc.Get("send.preview.telegram");
            TxtPreviewDiscordLabel.Text = _loc.Get("send.preview.discord");
            TxtPreviewEmpty.Text = _loc.Get("send.preview.empty");
            TxtThumbnailNote.Text = _loc.Get("send.thumbnail.twitch_note");
            BtnSend.Content = _loc.Get("send.button");

            ItemLive.Content = _loc.Get("type.live");
            ItemUpcoming.Content = _loc.Get("type.upcoming");
            ItemVideo.Content = _loc.Get("type.video");
            ItemShort.Content = _loc.Get("type.short");

            if (BtnTogglePicker.Tag as string != "selected")
                BtnTogglePicker.Content = _loc.Get("send.picker.toggle");

            UpdateDateTimeLabels();

            var active = new SolidColorBrush(Color.FromRgb(0x8A, 0x46, 0xC1));
            var inactive = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x25));
            BtnUA.Background = _loc.Language == UiLanguage.UA ? active : inactive;
            BtnEN.Background = _loc.Language == UiLanguage.EN ? active : inactive;

            if (_isReady) UpdatePreview();
        }

        private void BtnUA_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.UA);

        private void BtnEN_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.EN);

        private async Task LoadCandidatesAsync()
        {
            if (_youTubeService == null) return;

            TxtPickerStatus.Visibility = Visibility.Visible;
            TxtPickerStatus.Text = _loc.Get("send.picker.loading");
            BtnTogglePicker.Content = _loc.Get("send.picker.toggle");

            try
            {
                _candidates = await _youTubeService.GetCandidatesAsync();
                BuildCandidateItems();
                TxtPickerStatus.Visibility = Visibility.Collapsed;

                if (ChkAutoLatest.IsChecked == true)
                    AutoFillBestPick();
                else
                    UpdatePreview();
            }
            catch
            {
                TxtPickerStatus.Text = _loc.Get("send.picker.error");
            }
        }

        // UA: Дата/час для показу в списку кандидатів — пріоритет:
        //     заплановано (Upcoming) > реальний старт трансляції > публікація
        // EN: Date/time to display in the candidate list — priority:
        //     scheduled (Upcoming) > real broadcast start > publish date
        private static long? BestDisplayTime(ContentCacheEntry entry) =>
            entry.Type == VideoType.Upcoming
                ? entry.ScheduledStartTime
                : entry.ActualStartTime ?? entry.PublishedAt;

        private void BuildCandidateItems()
        {
            var items = new List<CandidateItem>();

            foreach (var entry in _candidates)
            {
                BitmapImage? thumbnail = null;
                if (!string.IsNullOrEmpty(entry.ThumbnailUrl))
                {
                    try { thumbnail = new BitmapImage(new Uri(entry.ThumbnailUrl, UriKind.Absolute)); }
                    catch { thumbnail = null; }
                }

                var typeLabel = entry.Type switch
                {
                    VideoType.Live => _loc.Get("type.live"),
                    VideoType.Upcoming => _loc.Get("type.upcoming"),
                    VideoType.Video => _loc.Get("type.video"),
                    VideoType.Short => _loc.Get("type.short"),
                    _ => "?"
                };

                var displayTime = BestDisplayTime(entry);
                string dateLabel;
                if (displayTime.HasValue)
                {
                    var (d, t) = FormatKyivTime(displayTime.Value);
                    dateLabel = $"{d} {t}";
                }
                else
                {
                    dateLabel = DateTimeOffset.FromUnixTimeSeconds(entry.DiscoveredAt)
                        .ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                }

                items.Add(new CandidateItem
                {
                    Source = entry,
                    Thumbnail = thumbnail,
                    SubLabel = $"{typeLabel} · {dateLabel}"
                });
            }

            CandidatesList.ItemsSource = items;
        }

        private void ChkAutoLatest_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelPickerRow == null) return;
            bool auto = ChkAutoLatest.IsChecked == true;
            PanelPickerRow.Visibility = auto ? Visibility.Collapsed : Visibility.Visible;

            if (auto)
            {
                PanelPickerList.Visibility = Visibility.Collapsed;
                AutoFillBestPick();
            }
        }

        private void BtnTogglePicker_Click(object sender, RoutedEventArgs e)
        {
            PanelPickerList.Visibility = PanelPickerList.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AutoFillBestPick()
        {
            if (_candidates.Count == 0) return;

            var pick = _candidates.FirstOrDefault(c => c.Type == VideoType.Live)
                ?? _candidates.FirstOrDefault(c => c.Type == VideoType.Upcoming)
                ?? _candidates.FirstOrDefault(c => c.Type is VideoType.Video or VideoType.Short);

            if (pick != null) FillForm(pick);
            else UpdatePreview();
        }

        private void CandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CandidatesList.SelectedItem is CandidateItem item)
            {
                FillForm(item.Source);
                PanelPickerList.Visibility = Visibility.Collapsed;
                BtnTogglePicker.Tag = "selected";
                BtnTogglePicker.Content = $"🎬 {item.Title} — {_loc.Get("send.picker.change")}";
            }
        }

        // UA: Підставити дані обраного кандидата у форму — мініатюра береться
        //     напряму з ContentCacheEntry (найнадійніше джерело), а не через
        //     розпізнавання URL
        // EN: Populate the form from the selected candidate — the thumbnail
        //     is taken directly from ContentCacheEntry (the most reliable
        //     source), not via URL pattern matching
        private void FillForm(ContentCacheEntry entry)
        {
            TitleInput.Text = entry.Title;

            TypeCombo.SelectedItem = entry.Type switch
            {
                VideoType.Live => ItemLive,
                VideoType.Upcoming => ItemUpcoming,
                VideoType.Video => ItemVideo,
                VideoType.Short => ItemShort,
                _ => ItemVideo
            };

            UrlInput.Text = entry.Url;

            var displayTime = BestDisplayTime(entry);
            if (displayTime.HasValue)
            {
                var (d, t) = FormatKyivTime(displayTime.Value);
                DateInput.Text = d;
                TimeInput.Text = t;
            }
            else
            {
                DateInput.Text = string.Empty;
                TimeInput.Text = string.Empty;
            }

            SetThumbnail(entry.ThumbnailUrl, isTwitch: false);
            UpdatePreview();
        }

        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDateTimeLabels();
            UpdatePreview();
        }

        // UA: Підпис полів дати/часу залежить від типу: обов'язкова дата
        //     початку для Upcoming, довідкова дата публікації для решти
        // EN: Date/time field labels depend on type: required start date for
        //     Upcoming, informational publish date for everything else
        private void UpdateDateTimeLabels()
        {
            if (TypeCombo?.SelectedItem is not ComboBoxItem item) return;
            var type = Enum.Parse<VideoType>((string)item.Tag);

            if (type == VideoType.Upcoming)
            {
                TxtDateLabel.Text = _loc.Get("send.date_label");
                TxtTimeLabel.Text = _loc.Get("send.time_label");
            }
            else
            {
                TxtDateLabel.Text = _loc.Get("send.date_label.published");
                TxtTimeLabel.Text = _loc.Get("send.time_label.published");
            }
        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
            => UpdatePreview();

        // UA: Окремий обробник для URL — окрім оновлення прикладу, намагається
        //     розпізнати платформу (для мініатюри й м'якого попередження) і,
        //     якщо посилання збігається з відомим кандидатом, підтягує решту
        //     полів автоматично (лише якщо заголовок ще порожній — щоб не
        //     затирати те, що користувач уже ввів вручну)
        // EN: A dedicated handler for the URL field — besides updating the
        //     preview, it tries to recognize the platform (for the thumbnail
        //     and soft warning) and, if the link matches a known candidate,
        //     auto-fills the rest of the fields (only if the title is still
        //     empty — to avoid overwriting something the user already typed)
        private void UrlInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isReady) return;

            var url = UrlInput.Text.Trim();

            TxtUrlWarning.Visibility = (!string.IsNullOrEmpty(url) && !IsRecognizedPlatformUrl(url))
                ? Visibility.Visible : Visibility.Collapsed;

            var ytId = TryExtractYouTubeVideoId(url);
            if (ytId != null)
            {
                var match = _candidates.FirstOrDefault(c => c.VideoId == ytId);
                if (match != null && string.IsNullOrWhiteSpace(TitleInput.Text))
                {
                    FillForm(match);
                    return;
                }

                SetThumbnail($"https://img.youtube.com/vi/{ytId}/mqdefault.jpg", isTwitch: false);
                UpdatePreview();
                return;
            }

            var twitchLogin = TryExtractTwitchLogin(url);
            if (twitchLogin != null)
            {
                // UA: Офіційного способу отримати превью Twitch-каналу без
                //     авторизації через Twitch API не існує (Helix API
                //     вимагає App Access Token). Перевірене на практиці
                //     неофіційне джерело (static-cdn.jtvnw.net) виявилось
                //     нестабільним/недоступним — тому прев'ю тут свідомо
                //     не показуємо, лише розпізнаємо посилання як коректне
                //     (знімаємо м'яке попередження про "непізнаний URL")
                // EN: There's no official way to get a Twitch channel
                //     thumbnail without Twitch API authorization (the Helix
                //     API requires an App Access Token). The unofficial
                //     source (static-cdn.jtvnw.net) tested in practice turned
                //     out unreliable/unavailable — so we deliberately don't
                //     show a thumbnail here, we just recognize the link as
                //     valid (clearing the "unrecognized URL" soft warning)
                SetThumbnail(null, isTwitch: false);
                UpdatePreview();
                return;
            }

            SetThumbnail(null, isTwitch: false);
            UpdatePreview();
        }

        // UA: Розпізнати YouTube video ID з будь-якого поширеного формату URL —
        //     не потребує API, превью будується напряму за ID
        // EN: Recognize a YouTube video ID from any common URL format —
        //     no API needed, the thumbnail is built directly from the ID
        private static string? TryExtractYouTubeVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var match = Regex.Match(url,
                @"(?:youtube\.com\/(?:watch\?v=|shorts\/|live\/)|youtu\.be\/)([A-Za-z0-9_-]{6,})");
            return match.Success ? match.Groups[1].Value : null;
        }

        // UA: Розпізнати логін каналу з посилання на Twitch
        // EN: Recognize a channel login from a Twitch link
        private static string? TryExtractTwitchLogin(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (!uri.Host.EndsWith("twitch.tv", StringComparison.OrdinalIgnoreCase)) return null;
            if (uri.Host.StartsWith("clips.", StringComparison.OrdinalIgnoreCase)) return null;

            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length == 0 || string.IsNullOrWhiteSpace(segments[0])) return null;

            var reserved = new[] { "videos", "directory", "settings", "subscriptions", "p", "downloads", "jobs", "turbo", "prime", "wallet" };
            if (reserved.Contains(segments[0].ToLowerInvariant())) return null;

            return segments[0];
        }

        private static bool IsRecognizedPlatformUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var host = uri.Host.ToLowerInvariant();
            return host.Contains("youtube.com") || host.Contains("youtu.be") || host.Contains("twitch.tv");
        }

        // UA: Встановити мініатюру безпечно — якщо посилання виявиться
        //     нечинним (особливо ймовірно для неофіційного Twitch-URL),
        //     просто ховаємо картинку без помилки
        // EN: Set the thumbnail safely — if the link turns out to be invalid
        //     (especially likely for the unofficial Twitch URL), just hide
        //     the image without an error
        private void SetThumbnail(string? url, bool isTwitch)
        {
            if (string.IsNullOrEmpty(url))
            {
                PreviewThumbnail.Source = null;
                PanelThumbnail.Visibility = Visibility.Collapsed;
                TxtThumbnailNote.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.DownloadFailed += (s, e) =>
                {
                    PreviewThumbnail.Source = null;
                    PanelThumbnail.Visibility = Visibility.Collapsed;
                    TxtThumbnailNote.Visibility = Visibility.Collapsed;
                };

                PreviewThumbnail.Source = bitmap;
                PanelThumbnail.Visibility = Visibility.Visible;
                TxtThumbnailNote.Visibility = isTwitch ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                PreviewThumbnail.Source = null;
                PanelThumbnail.Visibility = Visibility.Collapsed;
                TxtThumbnailNote.Visibility = Visibility.Collapsed;
            }
        }

        // UA: Живий приклад для обох платформ одночасно — окремо для Telegram
        //     і Discord, кожен рендериться зі свого шаблону
        // EN: Live preview for both platforms at once — separately for
        //     Telegram and Discord, each rendered from its own template
        private void UpdatePreview()
        {
            if (!_isReady || TypeCombo.SelectedItem is not ComboBoxItem item) return;

            var type = Enum.Parse<VideoType>((string)item.Tag);
            long? scheduled = type == VideoType.Upcoming
                ? ParseKyivTime(DateInput.Text, TimeInput.Text)
                : null;

            var templates = _templateService.GetTemplates();
            var twitchUrl = _settings.UseTwitch ? _settings.TwitchUrl : string.Empty;
            var url = UrlInput.Text.Trim();
            var title = TitleInput.Text.Trim();

            if (_settings.UseTelegram)
            {
                var template = type switch
                {
                    VideoType.Live => templates.Telegram.Live,
                    VideoType.Upcoming => templates.Telegram.Upcoming,
                    VideoType.Video => templates.Telegram.Video,
                    VideoType.Short => templates.Telegram.Short,
                    _ => string.Empty
                };
                PreviewTelegramBox.Text = _templateService.Apply(template, title, url, twitchUrl, scheduled);
            }

            if (_settings.UseDiscord)
            {
                var titleTemplate = type switch
                {
                    VideoType.Live => templates.DiscordTitles.Live,
                    VideoType.Upcoming => templates.DiscordTitles.Upcoming,
                    VideoType.Video => templates.DiscordTitles.Video,
                    VideoType.Short => templates.DiscordTitles.Short,
                    _ => string.Empty
                };
                var bodyTemplate = type switch
                {
                    VideoType.Live => templates.Discord.Live,
                    VideoType.Upcoming => templates.Discord.Upcoming,
                    VideoType.Video => templates.Discord.Video,
                    VideoType.Short => templates.Discord.Short,
                    _ => string.Empty
                };
                var renderedTitle = _templateService.Apply(titleTemplate, title, url, twitchUrl, scheduled);
                var renderedBody = _templateService.Apply(bodyTemplate, title, url, twitchUrl, scheduled);
                PreviewDiscordBox.Text = $"{renderedTitle}\n\n{renderedBody}";
            }
        }

        private static (string date, string time) FormatKyivTime(long unixSeconds)
        {
            var kyivZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            var kyivTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), kyivZone);
            return (
                kyivTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                kyivTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            );
        }

        private static long? ParseKyivTime(string dateText, string timeText)
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
                    _loc.Get("send.validation.title"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var type = Enum.Parse<VideoType>((string)((ComboBoxItem)TypeCombo.SelectedItem).Tag);
            long? scheduled = null;

            // UA: Дата/час обов'язкові й підставляються в сповіщення лише для
            //     Upcoming — для решти типів це поле лишається довідковим і
            //     нікуди не передається
            // EN: Date/time are required and are substituted into the
            //     notification only for Upcoming — for other types the field
            //     stays informational and isn't passed anywhere
            if (type == VideoType.Upcoming)
            {
                scheduled = ParseKyivTime(DateInput.Text, TimeInput.Text);
                if (scheduled == null)
                {
                    System.Windows.MessageBox.Show(
                        _loc.Get("send.validation.datetime"), _loc.Get("app.name"),
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
                    _loc.Get("send.sent"), _loc.Get("app.name"),
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

        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}