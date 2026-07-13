using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Caching.Memory;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebMail.Web.Models;

namespace WebMail.Web.Services;

public class MailService : IDisposable, IAsyncDisposable
{
    private readonly MailConfigService _configService;
    private readonly IMemoryCache _cache;
    private ImapClient _imapClient;

    public MailService(MailConfigService configService, IMemoryCache memoryCache)
    {
        _configService = configService;
        _cache = memoryCache;
    }

    // --- مدیریت اتصال (ایده ۴: Connection Reuse) ---
    // این متد بررسی می‌کند اگر اتصال باز است از همان استفاده کند، در غیر این صورت متصل شود
    private async Task<ImapClient> GetConnectedClientAsync(string email, string password)
    {
        if (_imapClient != null && _imapClient.IsConnected && _imapClient.IsAuthenticated)
        {
            return _imapClient;
        }

        var config = _configService.GetSettings();
        _imapClient = new ImapClient();

        await _imapClient.ConnectAsync(config.ImapServer, config.ImapPort, config.UseSsl);
        await _imapClient.AuthenticateAsync(email, password);

        return _imapClient;
    }

    // متد کمکی برای یافتن پوشه استاندارد
    private IMailFolder GetTargetFolder(ImapClient client, string folder)
    {
        var folderName = folder?.ToLower();

        if (folderName == "sent")
        {
            var sentFolder = client.GetFolder(SpecialFolder.Sent);
            if (sentFolder != null) return sentFolder;
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            return personal.GetSubfolder("Sent") ?? personal.GetSubfolder("Sent Items") ?? client.Inbox;
        }
        else if (folderName == "drafts")
        {
            var draftsFolder = client.GetFolder(SpecialFolder.Drafts);
            if (draftsFolder != null) return draftsFolder;
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            return personal.GetSubfolder("Drafts") ?? client.Inbox;
        }
        else if (folderName == "trash")
        {
            var trashFolder = client.GetFolder(SpecialFolder.Trash);
            if (trashFolder != null) return trashFolder;
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            return personal.GetSubfolder("Trash") ?? personal.GetSubfolder("Deleted Items") ?? personal.GetSubfolder("Deleted Messages") ?? client.Inbox;
        }

        return client.Inbox;
    }

    // --- دریافت لیست ایمیل‌ها با قابلیت کش (ایده ۳) ---
    public async Task<IList<IMessageSummary>> GetEmailListAsync(string email, string password, string folder = "inbox")
    {
        // کلید کش بر اساس ایمیل و نام پوشه
        var cacheKey = $"EmailList_{email}_{folder.ToLower()}";

        // بررسی موجود بودن در کش (مدت اعتبار: ۳ دقیقه)
        if (_cache.TryGetValue(cacheKey, out IList<IMessageSummary> cachedMessages))
        {
            return cachedMessages;
        }

        var client = await GetConnectedClientAsync(email, password);
        var targetFolder = GetTargetFolder(client, folder);

        // اگر پوشه از قبل باز است و دسترسی متفاوتی نیاز داریم، مدیریت می‌شود
        if (!targetFolder.IsOpen)
            await targetFolder.OpenAsync(FolderAccess.ReadOnly);

        int minIndex = Math.Max(0, targetFolder.Count - 20);
        int maxIndex = Math.Max(0, targetFolder.Count - 1);

        IList<IMessageSummary> messages = new List<IMessageSummary>();

        if (targetFolder.Count > 0)
        {
            messages = await targetFolder.FetchAsync(minIndex, maxIndex,
                MessageSummaryItems.Envelope |
                MessageSummaryItems.UniqueId |
                MessageSummaryItems.Flags);
        }

        var result = messages.OrderByDescending(m => m.Index).ToList();

        // ذخیره در کش برای دفعات بعدی
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(3));

        return result;
    }

    // دریافت خلاصه صندوق ورودی (ارجاع به متد لیست)
    public Task<IList<IMessageSummary>> GetInboxSummaryAsync(string email, string password)
    {
        return GetEmailListAsync(email, password, "inbox");
    }

    // --- دریافت جزئیات ایمیل با قابلیت کش (ایده ۳) ---
    public async Task<EmailDetailViewModel> GetFastEmailDetailAsync(string email, string password, uint uid, string folder = "inbox")
    {
        var cacheKey = $"EmailDetail_{email}_{folder.ToLower()}_{uid}";

        if (_cache.TryGetValue(cacheKey, out EmailDetailViewModel cachedDetail))
        {
            return cachedDetail;
        }

        var client = await GetConnectedClientAsync(email, password);
        var targetFolder = GetTargetFolder(client, folder);

        if (!targetFolder.IsOpen)
            await targetFolder.OpenAsync(FolderAccess.ReadWrite);

        var uniqueId = new UniqueId(uid);
        var summaries = await targetFolder.FetchAsync(new[] { uniqueId }, MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure | MessageSummaryItems.Flags);
        var summary = summaries.FirstOrDefault();

        if (summary == null) return null;

        var mailbox = summary.Envelope.From.Mailboxes.FirstOrDefault();
        var viewModel = new EmailDetailViewModel
        {
            Subject = string.IsNullOrEmpty(summary.Envelope.Subject) ? "(بدون موضوع)" : summary.Envelope.Subject,
            SenderName = mailbox?.Name ?? "ناشناس",
            SenderAddress = mailbox?.Address ?? "",
            Date = summary.Envelope.Date.HasValue ? summary.Envelope.Date.Value.LocalDateTime : DateTime.Now
        };

        if (summary.TextBody != null)
        {
            var textPart = (TextPart)await targetFolder.GetBodyPartAsync(uniqueId, summary.TextBody);
            viewModel.TextBody = textPart.Text;
        }

        if (summary.HtmlBody != null)
        {
            var htmlPart = (TextPart)await targetFolder.GetBodyPartAsync(uniqueId, summary.HtmlBody);
            var htmlContent = htmlPart.Text;

            var inlineAttachments = summary.BodyParts.Where(x => !string.IsNullOrEmpty(x.ContentId)).ToList();
            foreach (var inline in inlineAttachments)
            {
                var mimePart = (MimePart)await targetFolder.GetBodyPartAsync(uniqueId, inline);
                using var memoryStream = new MemoryStream();
                await mimePart.Content.DecodeToAsync(memoryStream);

                var base64 = Convert.ToBase64String(memoryStream.ToArray());
                var dataUri = $"data:{mimePart.ContentType.MimeType};base64,{base64}";
                htmlContent = htmlContent.Replace($"cid:{inline.ContentId}", dataUri);
            }

            viewModel.HtmlBody = htmlContent;
        }

        // اگر ایمیل خوانده نشده بود، به خوانده شده تغییر وضعیت بده و کش لیست را باطل کن
        if (!summary.Flags.Value.HasFlag(MessageFlags.Seen))
        {
            await targetFolder.AddFlagsAsync(uniqueId, MessageFlags.Seen, true);
            _cache.Remove($"EmailList_{email}_{folder.ToLower()}"); // پاک کردن کش لیست برای آپدیت تیک خوانده شده
            _cache.Remove($"Stats_{email}"); // پاک کردن کش وضعیت (تعداد ایمیل‌های نخوانده)
        }

        // ذخیره جزئیات ایمیل در کش برای ۱۵ دقیقه
        _cache.Set(cacheKey, viewModel, TimeSpan.FromMinutes(15));

        return viewModel;
    }

    public Task<EmailDetailViewModel> GetFastEmailDetailAsync(string email, string password, uint uid)
    {
        return GetFastEmailDetailAsync(email, password, uid, "inbox");
    }

    public async Task<MimeMessage> GetMessageAsync(string email, string password, uint uid)
    {
        var client = await GetConnectedClientAsync(email, password);
        if (!client.Inbox.IsOpen)
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly);

        return await client.Inbox.GetMessageAsync(new UniqueId(uid));
    }

    // --- حذف ایمیل‌ها و باطل کردن کش ---
    public async Task DeleteMessagesAsync(string email, string password, List<uint> uids, string folder = "inbox")
    {
        if (uids == null || !uids.Any()) return;

        var client = await GetConnectedClientAsync(email, password);
        var targetFolder = GetTargetFolder(client, folder);

        if (!targetFolder.IsOpen)
            await targetFolder.OpenAsync(FolderAccess.ReadWrite);

        var uniqueIds = uids.Select(id => new UniqueId(id)).ToList();

        if (folder?.ToLower() == "trash")
        {
            await targetFolder.AddFlagsAsync(uniqueIds, MessageFlags.Deleted, true);
            await targetFolder.ExpungeAsync();
        }
        else
        {
            var trashFolder = GetTargetFolder(client, "trash");

            if (trashFolder != null && targetFolder.FullName != trashFolder.FullName)
            {
                await targetFolder.MoveToAsync(uniqueIds, trashFolder);
            }
            else
            {
                await targetFolder.AddFlagsAsync(uniqueIds, MessageFlags.Deleted, true);
                await targetFolder.ExpungeAsync();
            }
        }

        // باطل کردن (Invalidate) کش پوشه فعلی و زباله‌دان تا اطلاعات جدید لود شود
        _cache.Remove($"EmailList_{email}_{folder.ToLower()}");
        _cache.Remove($"EmailList_{email}_trash");
        _cache.Remove($"Stats_{email}");
    }

    // ارسال ایمیل (بدون نیاز به کش)
    public void SendEmail(string fromEmail, string password, string to, string subject, string body)
    {
        var config = _configService.GetSettings();
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromEmail, fromEmail));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = body };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        client.Connect(config.SmtpServer, config.SmtpPort, config.UseSsl);
        client.Authenticate(fromEmail, password);
        client.Send(message);
        client.Disconnect(true);
    }

    // دریافت آمار با قابلیت کش
    public async Task<(int InboxUnread, int DraftsTotal)> GetFolderStatsAsync(string email, string password)
    {
        var cacheKey = $"Stats_{email}";
        if (_cache.TryGetValue(cacheKey, out (int, int) cachedStats))
        {
            return cachedStats;
        }

        var client = await GetConnectedClientAsync(email, password);

        await client.Inbox.StatusAsync(StatusItems.Unread);
        int inboxUnread = client.Inbox.Unread;

        var draftsFolder = GetTargetFolder(client, "drafts");
        await draftsFolder.StatusAsync(StatusItems.Count);
        int draftsTotal = draftsFolder.Count;

        var stats = (inboxUnread, draftsTotal);
        _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(2));

        return stats;
    }

    // --- آزادسازی منابع (Clean up) ---
    public void Dispose()
    {
        if (_imapClient != null)
        {
            if (_imapClient.IsConnected) _imapClient.Disconnect(true);
            _imapClient.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_imapClient != null)
        {
            if (_imapClient.IsConnected) await _imapClient.DisconnectAsync(true);
            _imapClient.Dispose();
        }
    }
}