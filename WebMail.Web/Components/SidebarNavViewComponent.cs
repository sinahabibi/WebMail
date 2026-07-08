using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using WebMail.Web.Services;

namespace WebMail.Web.Components
{
    public class SidebarNavViewComponent : ViewComponent
    {
        private readonly MailService _mailService;
        private readonly IDataProtector _protector;

        public SidebarNavViewComponent(MailService mailService, IDataProtector protector)
        {
            _mailService = mailService;
            _protector = protector;
        }

        public async Task<IViewComponentResult> InvokeAsync(string currentFolder = "inbox")
        {
            // ذخیره نام پوشه فعلی برای استایل‌دهی (رنگ آبی منوی فعال)
            ViewBag.CurrentFolder = currentFolder?.ToLower() ?? "inbox";

            int inboxUnread = 0;
            int draftsTotal = 0;

            if (User.Identity.IsAuthenticated)
            {
                var email = UserClaimsPrincipal.FindFirst(ClaimTypes.Email)?.Value;
                var protectedPassword = UserClaimsPrincipal.FindFirst("MailPassword")?.Value;

                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(protectedPassword))
                {
                    try
                    {
                        var password = _protector.Unprotect(protectedPassword);

                        // دریافت آمار با بالاترین سرعت
                        var stats = await _mailService.GetFolderStatsAsync(email, password);
                        inboxUnread = stats.InboxUnread;
                        draftsTotal = stats.DraftsTotal;
                    }
                    catch { /* در صورت خطای شبکه، اعداد صفر می‌مانند */ }
                }
            }

            // ارسال اعداد به ویو (به صورت یک مدل داینامیک)
            return View(new { InboxUnread = inboxUnread, DraftsTotal = draftsTotal });
        }
    }
}