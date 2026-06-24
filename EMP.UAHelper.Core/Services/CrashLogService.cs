// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Сервіс для запису помилок та подій у лог-файл
// EN: Service for writing errors and events to a log file
namespace EMP.UAHelper.Core.Services
{
    public class CrashLogService
    {
        private readonly string _logPath;

        public CrashLogService()
        {
            // UA: Папка logs/ поруч з exe
            // EN: logs/ folder next to the exe
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            _logPath = Path.Combine(logsDir, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
        }

        // UA: Записати інформаційне повідомлення
        // EN: Write informational message
        public async Task LogInfoAsync(string message)
            => await WriteAsync("INFO", message);

        // UA: Записати попередження
        // EN: Write warning
        public async Task LogWarningAsync(string message)
            => await WriteAsync("WARN", message);

        // UA: Записати помилку з деталями виключення
        // EN: Write error with exception details
        public async Task LogErrorAsync(string message, Exception? ex = null)
        {
            await WriteAsync("ERROR", message);
            if (ex != null)
            {
                await WriteAsync("EXCEPTION", ex.ToString());
                if (ex.InnerException != null)
                    await WriteAsync("INNER", ex.InnerException.ToString());
            }
        }

        // UA: Базовий запис рядка в лог
        // EN: Base method to write a line to the log
        private async Task WriteAsync(string level, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            await File.AppendAllTextAsync(_logPath, line + Environment.NewLine);
        }
    }
}