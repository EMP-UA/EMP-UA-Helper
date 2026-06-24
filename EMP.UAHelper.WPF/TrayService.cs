// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс керування іконкою в треї та меню
// EN: Tray icon and context menu management service
using EMP.UAHelper.Core.Models;
using EMP.UAHelper.Core.Services;
using System.Drawing;
using System.Windows.Forms;

namespace EMP.UAHelper.WPF
{
    public class TrayService : IDisposable
    {
        private readonly YouTubeService _youTubeService;
        private readonly TelegramService _telegramService;
        private readonly DiscordService _discordService;
        private readonly CrashLogService _crashLogService;
        private readonly TemplateService _templateService;
        private readonly LocalizationService _loc;
        private NotifyIcon? _trayIcon;

        public TrayService(
            YouTubeService youTubeService,
            TelegramService telegramService,
            DiscordService discordService,
            CrashLogService crashLogService,
            TemplateService templateService,
            LocalizationService loc)
        {
            _youTubeService = youTubeService;
            _telegramService = telegramService;
            _discordService = discordService;
            _crashLogService = crashLogService;
            _templateService = templateService;
            _loc = loc;
        }

        // UA: Ініціалізація іконки та меню в треї
        // EN: Initialize tray icon and context menu
        public void Initialize()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Visible = true,
                Text = _loc.Get("app.name")
            };

            BuildMenu();

            // UA: При зміні мови перебудовуємо меню
            // EN: Rebuild menu on language change
            _loc.LanguageChanged += BuildMenu;

            _ = _crashLogService.LogInfoAsync(_loc.Get("tray.started"));
        }

        // UA: Побудова контекстного меню
        // EN: Build context menu
        private void BuildMenu()
        {
            if (_trayIcon == null) return;

            var menu = new ContextMenuStrip();

            menu.Items.Add(_loc.Get("tray.send"), null, async (s, e) =>
                await HandleNotificationAsync());

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(_loc.Get("tray.edit_templates"), null, (s, e) =>
                OpenTemplateEditor());

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(_loc.Get("tray.exit"), null, (s, e) =>
            {
                _trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });

            _trayIcon.ContextMenuStrip = menu;
        }

        // UA: Відкрити вікно редактора шаблонів
        // EN: Open template editor window
        private void OpenTemplateEditor()
        {
            var editor = new TemplateEditorWindow(_templateService, _loc);
            editor.Show();
        }

        // UA: Основна логіка — запит до YouTube і відправка сповіщень
        // EN: Main logic — YouTube query and sending notifications
        private async Task HandleNotificationAsync()
        {
            if (_trayIcon == null) return;

            try
            {
                _trayIcon.Text = _loc.Get("tray.checking");
                await _crashLogService.LogInfoAsync(_loc.Get("tray.checking"));

                var video = await _youTubeService.GetLatestContentAsync();

                if (video == null)
                {
                    await _crashLogService.LogWarningAsync(_loc.Get("tray.no_result"));
                    _trayIcon.ShowBalloonTip(3000, _loc.Get("app.name"),
                        _loc.Get("tray.no_result"),
                        ToolTipIcon.Warning);
                    return;
                }

                await _crashLogService.LogInfoAsync($"[{video.Type}] {video.Title} ({video.Url})");

                var typeLabel = video.Type switch
                {
                    VideoType.Live => _loc.Get("type.live"),
                    VideoType.Upcoming => _loc.Get("type.upcoming"),
                    VideoType.Video => _loc.Get("type.video"),
                    VideoType.Short => _loc.Get("type.short"),
                    _ => "?"
                };

                await _crashLogService.LogInfoAsync(_loc.Get("tray.sending"));

                await Task.WhenAll(
                    _telegramService.SendNotificationAsync(video),
                    _discordService.SendNotificationAsync(video)
                );

                await _crashLogService.LogInfoAsync(_loc.Get("tray.sent"));

                _trayIcon.ShowBalloonTip(3000, _loc.Get("app.name"),
                    $"{typeLabel}\n{video.Title}\n\n{_loc.Get("tray.sent")}",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                await _crashLogService.LogErrorAsync(_loc.Get("app.error"), ex);

                _trayIcon?.ShowBalloonTip(5000, _loc.Get("app.error"),
                    ex.Message,
                    ToolTipIcon.Error);
            }
            finally
            {
                if (_trayIcon != null)
                    _trayIcon.Text = _loc.Get("app.name");
            }
        }

        // UA: Звільняємо ресурси іконки
        // EN: Dispose tray icon resources
        public void Dispose()
        {
            _loc.LanguageChanged -= BuildMenu;
            _ = _crashLogService.LogInfoAsync(_loc.Get("tray.stopped"));
            _trayIcon?.Dispose();
        }
    }
}