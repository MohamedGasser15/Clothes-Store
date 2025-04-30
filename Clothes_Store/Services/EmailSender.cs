using Microsoft.AspNetCore.Identity.UI.Services;
using MailKit.Net.Smtp;
using MimeKit;
using MimeKit.Text;

namespace Clothes_Store.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;

        public EmailSender(IConfiguration config)
        {
            _host = config["GoogleSMTP:Host"];
            _port = config.GetValue<int>("GoogleSMTP:Port");
            _username = config["GoogleSMTP:Username"];
            _password = config["GoogleSMTP:Password"];
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailMessage = new MimeMessage();

            // Set From address
            emailMessage.From.Add(new MailboxAddress("LushThreads", "LushThreads15@gmail.com"));

            // Set To address
            emailMessage.To.Add(MailboxAddress.Parse(email));

            // Set subject and body
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(TextFormat.Html)
            {
                Text = htmlMessage
            };

            using var client = new SmtpClient();

            // Connect to Google's SMTP server
            await client.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.StartTls);

            // Authenticate with credentials
            await client.AuthenticateAsync(_username, _password);

            // Send email
            await client.SendAsync(emailMessage);

            // Disconnect
            await client.DisconnectAsync(true);
        }
    }
}