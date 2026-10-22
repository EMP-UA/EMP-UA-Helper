// =============================================================================
// EMP UA Helper — App.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Логіка запуску програми — ініціалізація сервісів і трею
// EN: Application startup logic — services and tray initialization
// =============================================================================
using EMP.UAHelper.Core.Services;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;

namespace EMP.UAHelper.WPF
{
    public partial class App : Application
    {
        private TrayService? _trayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // UA: Ініціалізація сервісу локалізації (UA за замовчуванням)
            // EN: Initialize localization service (UA by default)
            var localizationService = new LocalizationService();

            // UA: Перевірка, чи існує файл налаштувань
            // EN: Check if settings file exists
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath))
            {
                var firstRun = new FirstRunWindow(localizationService);
                if (firstRun.ShowDialog() != true)
                {
                    // UA: Вікно закрито без збереження — вихід
                    // EN: Window closed without saving — exit
                    Shutdown();
                    return;
                }
            }

            // UA: Завантаження конфігурації
            // EN: Load configuration
            var configService = new ConfigurationService();
            var settings = configService.Settings;

            // UA: Відновлення збереженої мови інтерфейсу
            // EN: Restore saved UI language
            if (settings.UiLanguage?.ToLower() == "en")
                localizationService.SetLanguage(UiLanguage.EN);

            // UA: Базові сервіси, побудова платформ делегована TrayService
            //     (через ContentDispatchFactory), щоб їх можна було
            //     перебудувати з вікна налаштувань без перезапуску
            // EN: Base services — platform construction is delegated to
            //     TrayService (via ContentDispatchFactory) so it can be
            //     rebuilt from the settings window without restarting
            var crashLogService = new CrashLogService();
            var templateService = new TemplateService();

            _trayService = new TrayService(settings, templateService, crashLogService, localizationService);
            _trayService.Initialize();

            // UA: За налаштуванням одразу відкривається вікно відправки
            // EN: Per the setting, the send window opens right away
            if (settings.OpenSendWindowOnStartup)
                _trayService.OpenSendNotification();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // UA: Іконка в треї прибирається при виході
            // EN: Remove tray icon on exit
            _trayService?.Dispose();
            base.OnExit(e);
        }
    }
}