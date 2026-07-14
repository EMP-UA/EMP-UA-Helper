// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Логіка вікна першого запуску
// EN: First run window code-behind
using EMP.UAHelper.Core.Services;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;

namespace EMP.UAHelper.WPF
{
    public partial class FirstRunWindow : Window
    {
        private readonly LocalizationService _loc;

        private readonly string _settingsPath =
            Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        public FirstRunWindow(LocalizationService loc)
        {
            InitializeComponent();

            // UA: Встановлюємо чекбокси ПРОГРАМНО, після InitializeComponent —
            //     якщо задати IsChecked="True" прямо в XAML, подія Checked
            //     спрацьовує під час побудови дерева елементів, коли пізніші
            //     за розташуванням панелі (PanelYoutube тощо) ще не присвоєні
            //     своїм полям, що спричиняє NullReferenceException
            // EN: Set checkboxes PROGRAMMATICALLY, after InitializeComponent —
            //     setting IsChecked="True" directly in XAML fires the Checked
            //     event while the element tree is still being built, at a
            //     point where panels declared later in the file (e.g.
            //     PanelYoutube) aren't yet assigned to their fields, causing
            //     a NullReferenceException
            ChkUseTelegram.IsChecked = true;
            ChkUseYoutube.IsChecked = true;
            ChkUseDiscord.IsChecked = true;
            ChkUseTwitch.IsChecked = true;

            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            Title = _loc.Get("firstrun.title");
            TxtWelcome.Text = _loc.Get("firstrun.welcome");
            TxtDescription.Text = _loc.Get("firstrun.description");
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
            BtnSave.Content = _loc.Get("firstrun.save");

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

        // UA: Захисні null-перевірки — додаткова страховка, навіть попри те,
        //     що чекбокси тепер встановлюються після InitializeComponent
        // EN: Defensive null checks — extra safety net, even though the
        //     checkboxes are now set after InitializeComponent
        private void ChkUseTelegram_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelTelegram == null) return;
            PanelTelegram.Visibility = ChkUseTelegram.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkUseYoutube_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelYoutube == null) return;
            PanelYoutube.Visibility = ChkUseYoutube.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkUseDiscord_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelDiscord == null) return;
            PanelDiscord.Visibility = ChkUseDiscord.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkUseTwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelTwitch == null) return;
            PanelTwitch.Visibility = ChkUseTwitch.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

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

            DialogResult = true;
            Close();
        }
    }
}