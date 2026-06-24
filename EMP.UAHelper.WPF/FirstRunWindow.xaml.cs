// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Логіка вікна першого запуску
// EN: First run window code-behind
using EMP.UAHelper.Core.Services;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace EMP.UAHelper.WPF
{
    public partial class FirstRunWindow : Window
    {
        private readonly LocalizationService _loc;

        // UA: Шлях до файлу налаштувань
        // EN: Path to settings file
        private readonly string _settingsPath =
            Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        public FirstRunWindow(LocalizationService loc)
        {
            InitializeComponent();
            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;
            ApplyLocalization();
        }

        // UA: Застосувати локалізацію до всіх елементів
        // EN: Apply localization to all elements
        private void ApplyLocalization()
        {
            Title = _loc.Get("firstrun.title");
            TxtWelcome.Text = _loc.Get("firstrun.welcome");
            TxtDescription.Text = _loc.Get("firstrun.description");
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

        // UA: Зберегти налаштування і закрити вікно
        // EN: Save settings and close window
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // UA: Валідація — всі поля обов'язкові
            // EN: Validation — all fields are required
            if (string.IsNullOrWhiteSpace(TelegramToken.Text) ||
                string.IsNullOrWhiteSpace(TelegramChannel.Text) ||
                TelegramChannel.Text.Trim() == "@" ||
                string.IsNullOrWhiteSpace(YoutubeKey.Text) ||
                string.IsNullOrWhiteSpace(YoutubeChannelId.Text) ||
                string.IsNullOrWhiteSpace(DiscordWebhook.Text) ||
                DiscordWebhook.Text.Trim() == "https://discord.com/api/webhooks/" ||
                string.IsNullOrWhiteSpace(DiscordRoleId.Text) ||
                string.IsNullOrWhiteSpace(TwitchUrl.Text) ||
                TwitchUrl.Text.Trim() == "https://www.twitch.tv/")
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("firstrun.validation"),
                    _loc.Get("app.name"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // UA: Формуємо об'єкт налаштувань
            // EN: Build settings object
            var settings = new
            {
                TelegramBotToken = TelegramToken.Text.Trim(),
                YoutubeApiKey = YoutubeKey.Text.Trim(),
                DiscordWebhookUrl = DiscordWebhook.Text.Trim(),
                DiscordRoleId = DiscordRoleId.Text.Trim(),
                ChannelId = YoutubeChannelId.Text.Trim(),
                ChannelUsername = TelegramChannel.Text.Trim(),
                TwitchUrl = TwitchUrl.Text.Trim(),
                UiLanguage = _loc.Language.ToString().ToLower()
            };

            var json = JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);

            DialogResult = true;
            Close();
        }
    }
}