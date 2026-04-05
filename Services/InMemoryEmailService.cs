using System;
using System.Threading.Tasks;

namespace Mitrayana.Api.Services
{
    [Obsolete("InMemoryEmailService has been removed. Configure SMTP and use SmtpEmailService instead.")]
    public class InMemoryEmailService : IEmailService
    {
        public Task SendAsync(string to, string subject, string htmlBody)
        {
            Console.WriteLine($"[InMemoryEmail] Would send email to {to} with subject '{subject}'");
            // throw new InvalidOperationException("InMemoryEmailService is deprecated. Configure a valid SMTP host and use SmtpEmailService.");
            return Task.CompletedTask;
        }
    }
}