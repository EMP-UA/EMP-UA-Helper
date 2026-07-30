// =============================================================================
// EMP UA Helper — SettingsWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Код вікна налаштувань — попередньо заповнене поточними значеннями,
//     секрети замасковані за замовчуванням, застосовує зміни одразу
//     через переданий callback
// EN: Settings window code-behind — pre-filled with current values,
//     secrets masked by default, applies changes immediately via the
//     provided callback
// =============================================================================
using EMP.UAHelper.Core.Services;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;

namespace EMP.UAHelper.WPF
{
    public partial class SettingsWindow : Window
    {
        private readonly LocalizationService _loc;
        private readonly Action<AppSettings> _onSaved;

        private readonly string _settingsPath =
            Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        public SettingsWindow(AppSettings current, LocalizationService loc, Action<AppSettings> onSaved)
        {
            InitializeComponent();
            _loc = loc;
            _onSaved = onSaved;
            _loc.LanguageChanged += ApplyLocalization;

            ApplyLocalization();
            LoadCurrentSettings(current);
        }

        // UA: Попередньо заповнюємо поля та чекбокси поточними значеннями.
        //     Секретні поля завжди відкриваються замасковані — незалежно від
        //     того, чи вони показувались відкритими минулого разу
        // EN: Pre-fill fields and checkboxes with current values.
        //     Secret fields always open masked — regardless of whether they
        //     were shown unmasked last time
        private void LoadCurrentSettings(AppSettings s)
        {
            // UA: Список зон і поточне значення — показуємо зону, яка
            //     РЕАЛЬНО зараз використовується (через AppTimeZone.Resolve),
            //     а не сирий s.TimeZoneId. Для старих appsettings.json без
            //     цього поля це буде Київ — саме той фолбек, що й діє
            //     насправді, тож користувач бачить правду, а не порожнє поле
            // EN: Zone list and current value — we show the zone that's
            //     ACTUALLY in effect right now (via AppTimeZone.Resolve),
            //     not the raw s.TimeZoneId. For an old appsettings.json
            //     without this field that's Kyiv — the same fallback that's
            //     really in effect, so the user sees the truth, not a blank field
            TimezoneCombo.ItemsSource = AppTimeZone.AllZones();
            TimezoneCombo.SelectedValue = AppTimeZone.Resolve(s.TimeZoneId).Id;

            ChkUseTelegram.IsChecked = s.UseTelegram;
            ChkUseYoutube.IsChecked = s.UseYouTube;
            ChkUseDiscord.IsChecked = s.UseDiscord;
            ChkUseTwitch.IsChecked = s.UseTwitch;

            TelegramTokenSecure.Password = s.TelegramBotToken;
            TelegramTokenPlain.Text = s.TelegramBotToken;
            TelegramChannel.Text = string.IsNullOrEmpty(s.ChannelUsername) ? "@" : s.ChannelUsername;

            YoutubeKeySecure.Password = s.YoutubeApiKey;
            YoutubeKeyPlain.Text = s.YoutubeApiKey;
            YoutubeChannelId.Text = s.ChannelId;

            var webhook = string.IsNullOrEmpty(s.DiscordWebhookUrl)
                ? "https://discord.com/api/webhooks/" : s.DiscordWebhookUrl;
            DiscordWebhookSecure.Password = webhook;
            DiscordWebhookPlain.Text = webhook;
            DiscordRoleId.Text = s.DiscordRoleId;
            DiscordWebhookName.Text = s.DiscordWebhookName;

            foreach (var extra in s.DiscordExtraWebhooks)
                AddChannelRow(extra.Name, extra.Url);

            TwitchUrl.Text = string.IsNullOrEmpty(s.TwitchUrl)
                ? "https://www.twitch.tv/" : s.TwitchUrl;

            PanelTelegram.Visibility = s.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
            PanelYoutube.Visibility = s.UseYouTube ? Visibility.Visible : Visibility.Collapsed;
            PanelDiscord.Visibility = s.UseDiscord ? Visibility.Visible : Visibility.Collapsed;
            PanelTwitch.Visibility = s.UseTwitch ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyLocalization()
        {
            Title = _loc.Get("settings.title");
            TxtHeader.Text = _loc.Get("settings.header");
            TxtDescription.Text = _loc.Get("settings.description");
            TxtGroupGeneral.Text = _loc.Get("settings.group.general");
            TxtTimezoneLabel.Text = _loc.Get("settings.timezone.label");
            TxtTimezoneHint.Text = _loc.Get("settings.timezone.hint");
            TxtGroupContent.Text = _loc.Get("settings.group.content");
            TxtGroupNotify.Text = _loc.Get("settings.group.notify");
            TxtSectionTelegram.Text = _loc.Get("firstrun.section.telegram");
            TxtSectionYoutube.Text = _loc.Get("firstrun.section.youtube");
            TxtSectionDiscord.Text = _loc.Get("firstrun.section.discord");
            TxtSectionTwitch.Text = _loc.Get("firstrun.section.twitch");
            TxtTgToken.Text = _loc.Get("firstrun.tg.token");
            TxtTgTokenHint.Text = _loc.Get("firstrun.tg.token.hint");
            TxtTgChannel.Text = _loc.Get("firstrun.tg.channel");
            TxtTgChannelHint.Text = _loc.Get("firstrun.tg.channel.hint");
            TxtYtKey.Text = _loc.Get("firstrun.yt.key");
            TxtYtKeyHint.Text = _loc.Get("firstrun.yt.key.hint");
            TxtYtChannelId.Text = _loc.Get("firstrun.yt.channelid");
            TxtYtChannelIdHint.Text = _loc.Get("firstrun.yt.channelid.hint");
            TxtDcWebhook.Text = _loc.Get("firstrun.dc.webhook");
            TxtDcWebhookHint.Text = _loc.Get("firstrun.dc.webhook.hint");
            TxtDcRoleId.Text = _loc.Get("firstrun.dc.roleid");
            TxtDcRoleIdHint.Text = _loc.Get("firstrun.dc.roleid.hint");
            TxtDcWebhookName.Text = _loc.Get("settings.dc.webhook_name");
            TxtDcWebhookNameHint.Text = _loc.Get("settings.dc.webhook_name.hint");
            TxtDcExtraChannels.Text = _loc.Get("settings.dc.extra_channels");
            TxtDcExtraChannelsHint.Text = _loc.Get("settings.dc.extra_channels.hint");
            BtnAddChannel.Content = _loc.Get("settings.dc.add_channel");
            TxtTwUrl.Text = _loc.Get("firstrun.tw.url");
            TxtTwUrlHint.Text = _loc.Get("firstrun.tw.url.hint");
            BtnSave.Content = _loc.Get("settings.save");

            ChkUseTelegram.Content = _loc.Get("firstrun.use.telegram");
            ChkUseYoutube.Content = _loc.Get("firstrun.use.youtube");
            ChkUseDiscord.Content = _loc.Get("firstrun.use.discord");
            ChkUseTwitch.Content = _loc.Get("firstrun.use.twitch");

            var active = new SolidColorBrush(Color.FromRgb(0x8A, 0x46, 0xC1));
            var inactive = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x25));
            BtnUA.Background = _loc.Language == UiLanguage.UA ? active : inactive;
            BtnEN.Background = _loc.Language == UiLanguage.EN ? active : inactive;
        }

        private void BtnUA_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.UA);

        private void BtnEN_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.EN);

        private void ChkUseTelegram_Changed(object sender, RoutedEventArgs e)
            => PanelTelegram.Visibility = ChkUseTelegram.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

        private void ChkUseYoutube_Changed(object sender, RoutedEventArgs e)
            => PanelYoutube.Visibility = ChkUseYoutube.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

        private void ChkUseDiscord_Changed(object sender, RoutedEventArgs e)
            => PanelDiscord.Visibility = ChkUseDiscord.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

        private void ChkUseTwitch_Changed(object sender, RoutedEventArgs e)
            => PanelTwitch.Visibility = ChkUseTwitch.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;

        private void TogglePasswordVisibility(PasswordBox secure, TextBox plain, Button toggleButton)
        {
            if (plain.Visibility == Visibility.Visible)
            {
                secure.Password = plain.Text;
                plain.Visibility = Visibility.Collapsed;
                secure.Visibility = Visibility.Visible;
                toggleButton.Content = "👁";
            }
            else
            {
                plain.Text = secure.Password;
                secure.Visibility = Visibility.Collapsed;
                plain.Visibility = Visibility.Visible;
                toggleButton.Content = "🙈";
            }
        }

        private void ToggleTelegramToken_Click(object sender, RoutedEventArgs e)
            => TogglePasswordVisibility(TelegramTokenSecure, TelegramTokenPlain, BtnToggleTelegramToken);

        private void ToggleYoutubeKey_Click(object sender, RoutedEventArgs e)
            => TogglePasswordVisibility(YoutubeKeySecure, YoutubeKeyPlain, BtnToggleYoutubeKey);

        private void ToggleDiscordWebhook_Click(object sender, RoutedEventArgs e)
            => TogglePasswordVisibility(DiscordWebhookSecure, DiscordWebhookPlain, BtnToggleDiscordWebhook);

        private static string SecureValue(PasswordBox secure, TextBox plain)
            => plain.Visibility == Visibility.Visible ? plain.Text : secure.Password;

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            bool useTelegram = ChkUseTelegram.IsChecked == true;
            bool useYoutube = ChkUseYoutube.IsChecked == true;
            bool useDiscord = ChkUseDiscord.IsChecked == true;
            bool useTwitch = ChkUseTwitch.IsChecked == true;

            var telegramToken = SecureValue(TelegramTokenSecure, TelegramTokenPlain);
            var youtubeKey = SecureValue(YoutubeKeySecure, YoutubeKeyPlain);
            var discordWebhook = SecureValue(DiscordWebhookSecure, DiscordWebhookPlain);

            if (!useTelegram && !useDiscord)
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("firstrun.validation.platform"),
                    _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool telegramInvalid = useTelegram && (
                string.IsNullOrWhiteSpace(telegramToken) ||
                string.IsNullOrWhiteSpace(TelegramChannel.Text) ||
                TelegramChannel.Text.Trim() == "@");

            bool youtubeInvalid = useYoutube && (
                string.IsNullOrWhiteSpace(youtubeKey) ||
                string.IsNullOrWhiteSpace(YoutubeChannelId.Text));

            bool discordInvalid = useDiscord && (
                string.IsNullOrWhiteSpace(discordWebhook) ||
                discordWebhook.Trim() == "https://discord.com/api/webhooks/" ||
                string.IsNullOrWhiteSpace(DiscordRoleId.Text));

            bool twitchInvalid = useTwitch && (
                string.IsNullOrWhiteSpace(TwitchUrl.Text) ||
                TwitchUrl.Text.Trim() == "https://www.twitch.tv/");

            if (telegramInvalid || youtubeInvalid || discordInvalid || twitchInvalid)
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("firstrun.validation"),
                    _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = new AppSettings
            {
                TelegramBotToken = telegramToken.Trim(),
                YoutubeApiKey = youtubeKey.Trim(),
                DiscordWebhookUrl = discordWebhook.Trim(),
                DiscordWebhookName = DiscordWebhookName.Text.Trim(),
                DiscordExtraWebhooks = CollectExtraChannels(),
                DiscordRoleId = DiscordRoleId.Text.Trim(),
                ChannelId = YoutubeChannelId.Text.Trim(),
                ChannelUsername = TelegramChannel.Text.Trim(),
                TwitchUrl = TwitchUrl.Text.Trim(),
                UiLanguage = _loc.Language.ToString().ToLower(),
                TimeZoneId = TimezoneCombo.SelectedValue as string ?? AppTimeZone.FallbackId,
                UseTelegram = useTelegram,
                UseYouTube = useYoutube,
                UseDiscord = useDiscord,
                UseTwitch = useTwitch
            };

            var json = JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);

            _onSaved(settings);

            Close();
        }

        // =====================================================================
        // UA: Додаткові Discord-канали. Рядки будуються з коду, бо їх кількість
        //     довільна — описати їх статично в XAML неможливо. Кожен рядок це
        //     назва + URL + кнопка видалення; жодного зайвого стану не
        //     зберігаємо, значення читаються прямо з полів при збереженні.
        //     URL тут відкритий, а не під PasswordBox, на відміну від
        //     основного вебхука. Це свідомо: приховане поле, яке не можна
        //     звірити очима, при кількох схожих URL перетворює вибір каналу на
        //     вгадування, а сам файл appsettings.json усе одно лежить у
        //     відкритому вигляді поруч з exe — тобто приховування в UI тут
        //     дало б відчуття захисту, а не захист.
        // EN: Additional Discord channels. Rows are built from code because
        //     their count is arbitrary — they can't be declared statically in
        //     XAML. Each row is name + URL + a delete button; no extra state is
        //     kept, values are read straight from the fields on save.
        //     The URL here is shown in the clear rather than behind a
        //     PasswordBox, unlike the primary webhook. That's deliberate: a
        //     masked field you can't verify by eye turns picking between
        //     several similar URLs into guesswork, and appsettings.json itself
        //     sits in plain text next to the exe anyway — so masking here would
        //     provide the feeling of protection rather than protection.
        // =====================================================================
        private void BtnAddChannel_Click(object sender, RoutedEventArgs e)
            => AddChannelRow(string.Empty, string.Empty);

        private void AddChannelRow(string name, string url)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBox = new TextBox
            {
                Text = name,
                Style = (Style)FindResource("InputField"),
                Tag = "name"
            };
            Grid.SetColumn(nameBox, 0);

            var urlBox = new TextBox
            {
                Text = url,
                Style = (Style)FindResource("InputField"),
                Margin = new Thickness(6, 0, 0, 0),
                Tag = "url"
            };
            Grid.SetColumn(urlBox, 1);

            var removeButton = new Button
            {
                Content = "✕",
                Style = (Style)FindResource("RevealButton"),
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = _loc.Get("settings.dc.remove_channel")
            };
            Grid.SetColumn(removeButton, 2);
            removeButton.Click += (_, _) => PanelExtraChannels.Children.Remove(row);

            row.Children.Add(nameBox);
            row.Children.Add(urlBox);
            row.Children.Add(removeButton);

            PanelExtraChannels.Children.Add(row);
        }

        // UA: Рядки без URL відкидаємо мовчки — порожній рядок, який людина
        //     додала й передумала заповнювати, не має ставати "каналом", що
        //     нікуди не надсилає. Назва без URL сенсу не має, URL без назви —
        //     цілком (підставиться локалізоване "Канал без назви")
        // EN: Rows without a URL are dropped silently — an empty row the person
        //     added and then decided against shouldn't become a "channel" that
        //     sends nowhere. A name without a URL is meaningless; a URL without
        //     a name is fine (the localized "Unnamed channel" is used)
        private List<DiscordWebhookTarget> CollectExtraChannels()
        {
            var result = new List<DiscordWebhookTarget>();

            foreach (var row in PanelExtraChannels.Children.OfType<Grid>())
            {
                var boxes = row.Children.OfType<TextBox>().ToList();
                var name = boxes.FirstOrDefault(b => (b.Tag as string) == "name")?.Text.Trim() ?? string.Empty;
                var url = boxes.FirstOrDefault(b => (b.Tag as string) == "url")?.Text.Trim() ?? string.Empty;

                if (url.Length == 0) continue;

                result.Add(new DiscordWebhookTarget { Name = name, Url = url });
            }

            return result;
        }

        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}