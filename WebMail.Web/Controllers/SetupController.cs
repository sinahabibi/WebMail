using MailKit.Net.Imap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WebMail.Web.Models;
using WebMail.Web.Services;
using MailKit.Net.Smtp;

namespace WebMail.Web.Controllers
{
    public class SetupController : Controller
    {
        private readonly MailConfigService _configService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public SetupController(MailConfigService configService, IStringLocalizer<SharedResource> localizer)
        {
            _configService = configService;
            _localizer = localizer;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new ServerConfig { UseSsl = true });
        }

        // اکشن برای دکمه اصلی ذخیره (Save Settings)
        [HttpPost]
        public IActionResult Index(ServerConfig model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                // تست نهایی هر دو سرور قبل از ذخیره
                TestImapConnection(model);
                TestSmtpConnection(model);

                // ذخیره اطلاعات
                _configService.SaveSettings(model);
                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                // استفاده از ریسورس برای خطای اتصال
                ModelState.AddModelError("", $"{_localizer["connection_failed"]} {ex.Message}");
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult TestConnectionApi([FromBody] ServerConfig model)
        {
            // 1. تست IMAP
            try
            {
                using var imapClient = new MailKit.Net.Imap.ImapClient();
                imapClient.Timeout = 5000;
                imapClient.Connect(model.ImapServer, model.ImapPort, model.UseSsl);
                imapClient.Disconnect(true);
            }
            catch (Exception ex)
            {
                // پیام خطای دقیق که به مودال ارسال می‌شود
                return Json(new { success = false, title = _localizer["imap_connection_failed"].Value, message = ex.Message });
            }

            // 2. تست SMTP
            try
            {
                using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
                smtpClient.Timeout = 5000;
                smtpClient.Connect(model.SmtpServer, model.SmtpPort, model.UseSsl);
                smtpClient.Disconnect(true);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, title = _localizer["smtp_connection_failed"].Value, message = ex.Message });
            }

            // اگر هر دو موفقیت‌آمیز بودند
            return Json(new
            {
                success = true,
                title = _localizer["connection_successful"].Value,
                message = _localizer["connection_successful_msg"].Value
            });
        }

        private void TestImapConnection(ServerConfig model)
        {
            using var client = new MailKit.Net.Imap.ImapClient();
            client.Timeout = 5000;
            client.Connect(model.ImapServer, model.ImapPort, model.UseSsl);
            client.Disconnect(true);
        }

        private void TestSmtpConnection(ServerConfig model)
        {
            using var client = new MailKit.Net.Smtp.SmtpClient();
            client.Timeout = 5000;
            client.Connect(model.SmtpServer, model.SmtpPort, model.UseSsl);
            client.Disconnect(true);
        }
    }
}