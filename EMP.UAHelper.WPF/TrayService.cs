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
        private AppSettings _settings;
        private readonly TemplateService _templateService;
        private readonly CrashLogService _crashLogService;
        private readonly LocalizationService _loc;

        // UA: Перебудовуються при кожній зміні налаштувань платформ (ApplyServices)
        // EN: Rebuilt every time platform settings change (ApplyServices)
        private YouTubeService? _youTubeService;
        private NotificationDispatcher _dispatcher = null!;

        private NotifyIcon? _trayIcon;

        public TrayService(
            AppSettings settings,
            TemplateService templateService,
            CrashLogService crashLogService,
            LocalizationService loc)
        {
            _settings = settings;
            _templateService = templateService;
            _crashLogService = crashLogService;
            _loc = loc;
        }

        // UA: Ініціалізація іконки та меню в треї
        // EN: Initialize tray icon and context menu
        public void Initialize()
        {
            ApplyServices(_settings);

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

        // UA: (Пере)будувати сервіси платформ з поточних налаштувань
        // EN: (Re)build platform services from the current settings
        private void ApplyServices(AppSettings settings)
        {
            _settings = settings;
            var (youTube, dispatcher) = ContentDispatchFactory.Build(settings, _templateService, _crashLogService);
            _youTubeService = youTube;
            _dispatcher = dispatcher;
        }

        // UA: Викликається з вікна налаштувань після збереження — застосовує нову
        //     комбінацію платформ одразу, без перезапуску програми
        // EN: Called from the settings window after saving — applies the new
        //     platform combination immediately, without restarting the app
        public void ReloadSettings(AppSettings settings)
        {
            ApplyServices(settings);
            BuildMenu();
        }

        // UA: Побудова контекстного меню
        // EN: Build context menu
        private void BuildMenu()
        {
            if (_trayIcon == null) return;

            var menu = new ContextMenuStrip();

            // UA: Автоперевірка YouTube — лише якщо джерело увімкнене "сьогодні"
            // EN: YouTube auto-check — only if the source is enabled "today"
            if (_youTubeService != null)
            {
                menu.Items.Add(_loc.Get("tray.send"), null, async (s, e) =>
                    await HandleAutoNotificationAsync());
                menu.Items.Add(new ToolStripSeparator());
            }

            // UA: Ручне сповіщення доступне ЗАВЖДИ — незалежно від того, скільки
            //     платформ зараз увімкнено
            // EN: Manual notification is ALWAYS available — regardless of how
            //     many platforms are currently enabled
            menu.Items.Add(_loc.Get("tray.manual"), null, (s, e) =>
                OpenManualNotification());

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(_loc.Get("tray.settings"), null, (s, e) =>
                OpenSettings());

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

        private void OpenTemplateEditor()
        {
            var editor = new TemplateEditorWindow(_templateService, _loc);
            editor.Show();
        }

        private void OpenManualNotification()
        {
            var window = new ManualNotificationWindow(_dispatcher, _loc);
            window.Show();
        }

        // UA: Відкрити вікно налаштувань платформ — дозволяє змінити комбінацію
        //     Telegram/YouTube/Discord/Twitch будь-коли, без First Run заново
        // EN: Open the platform settings window — allows changing the
        //     Telegram/YouTube/Discord/Twitch combination anytime, without
        //     going through First Run again
        private void OpenSettings()
        {
            var window = new SettingsWindow(_settings, _loc, ReloadSettings);
            window.Show();
        }

        // UA: Автоматична гілка — запит до YouTube і відправка сповіщень
        // EN: Automatic branch — YouTube query and sending notifications
        private async Task HandleAutoNotificationAsync()
        {
            if (_trayIcon == null || _youTubeService == null) return;

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

                var typeLabel = video.Type switch
                {
                    VideoType.Live => _loc.Get("type.live"),
                    VideoType.Upcoming => _loc.Get("type.upcoming"),
                    VideoType.Video => _loc.Get("type.video"),
                    VideoType.Short => _loc.Get("type.short"),
                    _ => "?"
                };

                await _crashLogService.LogInfoAsync(_loc.Get("tray.sending"));

                await _dispatcher.SendAsync(video);

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