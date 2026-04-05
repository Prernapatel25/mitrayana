using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Mitrayana.Api.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }

    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public SmtpEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var smtp = _config.GetSection("Smtp");
            var host = smtp["Host"];
            if (string.IsNullOrWhiteSpace(host) || host.Contains("example.com"))
            {
                throw new InvalidOperationException("SMTP host is not configured. Set 'Smtp:Host' in appsettings.json to a real SMTP server host.");
            }

            var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var user = smtp["Username"];
            var pass = smtp["Password"];
            var from = smtp["From"] ?? "no-reply@mitrayana.local";
            var enableSsl = bool.TryParse(smtp["EnableSsl"], out var s) ? s : true;

            using (var client = new SmtpClient(host, port))
            {
                client.EnableSsl = enableSsl;
                if (!string.IsNullOrEmpty(user)) client.Credentials = new NetworkCredential(user, pass);

                var msg = new MailMessage(from, to, subject, htmlBody);
                msg.IsBodyHtml = true;

                await client.SendMailAsync(msg);
            }
        }
    }
}
