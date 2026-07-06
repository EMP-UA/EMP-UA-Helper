// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Код вікна налаштувань — попередньо заповнене поточними значеннями,
//     секрети замасковані за замовчуванням, застосовує зміни одразу
//     через переданий callback
// EN: Settings window code-behind — pre-filled with current values,
//     secrets masked by default, applies changes immediately via the
//     provided callback
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
                DiscordRoleId = DiscordRoleId.Text.Trim(),
                ChannelId = YoutubeChannelId.Text.Trim(),
                ChannelUsername = TelegramChannel.Text.Trim(),
                TwitchUrl = TwitchUrl.Text.Trim(),
                UiLanguage = _loc.Language.ToString().ToLower(),
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

        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}