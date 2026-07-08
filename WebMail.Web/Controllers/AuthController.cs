using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebMail.Web.Services;

namespace WebMail.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly MailConfigService _configService;
        private readonly IDataProtector _protector;

        public AuthController(MailConfigService configService, IDataProtector protector)
        {
            _configService = configService;
            _protector = protector;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var serverConfig = _configService.GetSettings();

            try
            {
                // ۱. احراز هویت در سرور IMAP
                using var client = new ImapClient();
                client.Connect(serverConfig.ImapServer, serverConfig.ImapPort, serverConfig.UseSsl);
                client.Authenticate(email, password);
                client.Disconnect(true);

                // ۲. رمزنگاری پسورد قبل از ذخیره در کوکی
                // این سرویس باید در Constructor کنترلر تزریق شود: IDataProtector _protector
                var protectedPassword = _protector.Protect(password);

                // ۳. ساخت کلیم‌ها (Claims)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, email),
                    new Claim(ClaimTypes.Email, email),
                    new Claim("MailPassword", protectedPassword) // پسورد رمزنگاری شده
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true, // ماندگاری لاگین
                    ExpiresUtc = DateTime.UtcNow.AddHours(2)
                };

                // ۴. ورود کاربر به سیستم ASP.NET
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToAction("Index", "Home");
            }
            catch (AuthenticationException)
            {
                ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "خطا در برقراری ارتباط با سرور.");
                return View();
            }
        }
}
}
