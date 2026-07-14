// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс керування іконкою в треї та меню
// EN: Tray icon and context menu management service
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
            _loc.LanguageChanged += BuildMenu;

            _ = _crashLogService.LogInfoAsync(_loc.Get("tray.started"));
        }

        private void ApplyServices(AppSettings settings)
        {
            _settings = settings;
            var (youTube, dispatcher) = ContentDispatchFactory.Build(settings, _templateService, _crashLogService);
            _youTubeService = youTube;
            _dispatcher = dispatcher;
        }

        // UA: Викликається з вікна налаштувань — застосовує нову комбінацію
        //     платформ одразу, без перезапуску
        // EN: Called from the settings window — applies the new platform
        //     combination immediately, without restarting
        public void ReloadSettings(AppSettings settings)
        {
            ApplyServices(settings);
            BuildMenu();
        }

        private void BuildMenu()
        {
            if (_trayIcon == null) return;

            var menu = new ContextMenuStrip();

            // UA: Єдиний пункт відправки — відкриває вікно з попереднім
            //     переглядом. Якщо YouTube увімкнено — доступний автопідбір
            //     та список кандидатів; якщо ні — лише ручний ввід.
            // EN: Single send entry point — opens a window with a preview.
            //     If YouTube is enabled — auto-pick and the candidate list
            //     are available; if not — manual entry only.
            menu.Items.Add(_loc.Get("tray.send"), null, (s, e) => OpenSendNotification());

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(_loc.Get("tray.settings"), null, (s, e) => OpenSettings());
            menu.Items.Add(_loc.Get("tray.edit_templates"), null, (s, e) => OpenTemplateEditor());

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

        private void OpenSendNotification()
        {
            var window = new SendNotificationWindow(_youTubeService, _dispatcher, _templateService, _settings, _loc);
            window.Show();
        }

        private void OpenSettings()
        {
            var window = new SettingsWindow(_settings, _loc, ReloadSettings);
            window.Show();
        }

        public void Dispose()
        {
            _loc.LanguageChanged -= BuildMenu;
            _ = _crashLogService.LogInfoAsync(_loc.Get("tray.stopped"));
            _trayIcon?.Dispose();
        }
    }
}