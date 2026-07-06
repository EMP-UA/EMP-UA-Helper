// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Логіка запуску програми — ініціалізація сервісів і трею
// EN: Application startup logic — services and tray initialization
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

            // UA: Ініціалізуємо сервіс локалізації (UA за замовчуванням)
            // EN: Initialize localization service (UA by default)
            var localizationService = new LocalizationService();

            // UA: Перевіряємо чи існує файл налаштувань
            // EN: Check if settings file exists
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath))
            {
                var firstRun = new FirstRunWindow(localizationService);
                if (firstRun.ShowDialog() != true)
                {
                    // UA: Користувач закрив вікно без збереження — виходимо
                    // EN: User closed window without saving — exit
                    Shutdown();
                    return;
                }
            }

            // UA: Завантажуємо конфігурацію
            // EN: Load configuration
            var configService = new ConfigurationService();
            var settings = configService.Settings;

            // UA: Відновлюємо збережену мову інтерфейсу
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // UA: Прибираємо іконку з трею при виході
            // EN: Remove tray icon on exit
            _trayService?.Dispose();
            base.OnExit(e);
        }
    }
}