namespace WebMail.Web.Models
{
    public class ServerConfig
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string ImapServer { get; set; }
        public int ImapPort { get; set; }
        public bool UseSsl { get; set; }
    }
    public class EmailDetailViewModel
    {
        public string Subject { get; set; }
        public string SenderName { get; set; }
        public string SenderAddress { get; set; }
        public DateTime Date { get; set; }
        public string HtmlBody { get; set; }
        public string TextBody { get; set; }
    }
}
