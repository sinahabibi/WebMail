using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using WebMail.Web.Services;

namespace WebMail.Web.Middlewares
{
    public class SetupMiddleware
    {
        private readonly RequestDelegate _next;

        public SetupMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, MailConfigService configService)
        {
            // اضافه کردن بررسی null برای جلوگیری از خطا و تبدیل به حروف کوچک
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var isSetupCompleted = configService.IsConfigured();

            // اجازه دسترسی به فایل‌های استاتیک و مسیر تغییر زبان (ChangeLanguage)
            if (path.StartsWith("/css") ||
                path.StartsWith("/js") ||
                path.StartsWith("/lib") ||
                path.StartsWith("/changelanguage") ||
                path.StartsWith("/changelanguage?") ||
                path.StartsWith("/home/changelanguage"))
            {
                await _next(context);
                return;
            }

            // اگر تنظیمات انجام نشده و کاربر در صفحه ستاپ نیست -> هدایت به ستاپ
            if (!isSetupCompleted && !path.StartsWith("/setup"))
            {
                context.Response.Redirect("/Setup");
                return;
            }

            // اگر تنظیمات انجام شده ولی کاربر می‌خواهد دوباره به ستاپ برود -> هدایت به صفحه اصلی
            if (isSetupCompleted && path.StartsWith("/setup"))
            {
                context.Response.Redirect("/");
                return;
            }

            await _next(context);
        }
    }
}