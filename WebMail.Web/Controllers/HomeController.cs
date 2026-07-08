using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;
using WebMail.Web.Models;
using WebMail.Web.Services;

namespace WebMail.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly MailService _mailService;
        private readonly IDataProtector _protector;

        public HomeController(MailService mailService, IDataProtector protector)
        {
            _mailService = mailService;
            _protector = protector;
        }

        public IActionResult Index()
        {
            ViewBag.CurrentFolder = "inbox";
            return View((EmailDetailViewModel)null);
        }

        // اکشن جدید برای صفحه ارسال شده‌ها
        public IActionResult Sent()
        {
            ViewBag.CurrentFolder = "sent";
            return View("Index", (EmailDetailViewModel)null);
        }

        [HttpGet]
        public async Task<IActionResult> Detail(uint uid, string folder = "inbox")
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var protectedPassword = User.FindFirst("MailPassword")?.Value;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(protectedPassword)) return RedirectToAction("Login", "Auth");
            var password = _protector.Unprotect(protectedPassword);

            // ارسال نام پوشه به سرویس
            var viewModel = await _mailService.GetFastEmailDetailAsync(email, password, uid, folder);

            ViewBag.CurrentFolder = folder; // ذخیره برای بازگشت به لیست صحیح
            return View("Index", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(List<uint> selectedUids, string folder = "inbox")
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var protectedPassword = User.FindFirst("MailPassword")?.Value;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(protectedPassword)) return RedirectToAction("Login", "Auth");
            var password = _protector.Unprotect(protectedPassword);

            await _mailService.DeleteMessagesAsync(email, password, selectedUids, folder);

            // هدایت کاربر به همان پوشه‌ای که در آن قرار داشت
            if (folder == "sent") return RedirectToAction("Sent");
            if (folder == "drafts") return RedirectToAction("Drafts");
            if (folder == "trash") return RedirectToAction("Trash"); // هدایت مجدد به زباله‌دان

            return RedirectToAction("Index");
        }

        // اکشن جدید برای صفحه پیش‌نویس‌ها
        public IActionResult Drafts()
        {
            ViewBag.CurrentFolder = "drafts";
            return View("Index", (EmailDetailViewModel)null);
        }

        public IActionResult Trash()
        {
            ViewBag.CurrentFolder = "trash";
            return View("Index", (EmailDetailViewModel)null);
        }

        [AllowAnonymous]
        [HttpPost]
        public IActionResult ChangeLanguage(string culture, string returnUrl)
        {
            // ذخیره زبان انتخاب شده در کوکی استاندارد ASP.NET
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl ?? "/");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
