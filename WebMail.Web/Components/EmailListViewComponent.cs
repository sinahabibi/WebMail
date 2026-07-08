using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using WebMail.Web.Services;
using MailKit;
using MailService = WebMail.Web.Services.MailService;

namespace WebMail.Web.Components
{
    public class EmailListViewComponent : ViewComponent
    {
        private readonly MailService _mailService;
        private readonly IDataProtector _protector;

        public EmailListViewComponent(MailService mailService, IDataProtector protector)
        {
            _mailService = mailService;
            _protector = protector;
        }

        public async Task<IViewComponentResult> InvokeAsync(string folder = "inbox")
        {
            ViewBag.CurrentFolder = folder; // پاس دادن پوشه به ویوی کامپوننت

            if (!User.Identity.IsAuthenticated) return View(new List<IMessageSummary>());

            var email = UserClaimsPrincipal.FindFirst(ClaimTypes.Email)?.Value;
            var protectedPassword = UserClaimsPrincipal.FindFirst("MailPassword")?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(protectedPassword))
                return View(new List<IMessageSummary>());

            try
            {
                var password = _protector.Unprotect(protectedPassword);
                // فراخوانی متد جدید
                var messages = await _mailService.GetEmailListAsync(email, password, folder);

                return View(messages);
            }
            catch
            {
                return View(new List<IMessageSummary>());
            }
        }
    }
}