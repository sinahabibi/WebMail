using System.Text.Json;
using WebMail.Web.Models;

namespace WebMail.Web.Services
{
    public class MailConfigService
    {
        private readonly string _filePath;

        public MailConfigService(IHostEnvironment env)
        {
            // فایل را در ریشه اصلی پروژه ذخیره می‌کنیم
            _filePath = Path.Combine(env.ContentRootPath, "mail-config.json");
        }

        // خواندن تنظیمات از فایل
        public ServerConfig GetSettings()
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ServerConfig>(json);
        }

        // ذخیره تنظیمات در فایل
        public void SaveSettings(ServerConfig settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_filePath, json);
        }

        // بررسی اینکه آیا قبلاً تنظیمات انجام شده است یا خیر
        public bool IsConfigured()
        {
            var settings = GetSettings();
            return settings != null && !string.IsNullOrEmpty(settings.SmtpServer);
        }
    }
}