using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;

namespace AppleEsportsErp.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(IUnitOfWork unitOfWork, ILogger<EmailService> logger, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to)) return;

            string host = _configuration["EmailSettings:Host"] ?? "";
            string portString = _configuration["EmailSettings:Port"] ?? "587";
            string username = _configuration["EmailSettings:Username"] ?? "";
            string password = _configuration["EmailSettings:Password"] ?? "";
            string fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@appleesports.com";

            // Override with global system config from UI if available
            var config = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.SystemConfig>().Query()
                .FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");

            if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(config.ConfigValue);
                    if (doc.RootElement.TryGetProperty("emailNotifications", out var emailNode))
                    {
                        if (emailNode.TryGetProperty("sender", out var senderNode) && !string.IsNullOrWhiteSpace(senderNode.GetString()))
                        {
                            username = senderNode.GetString()!;
                            fromEmail = username;
                        }
                        if (emailNode.TryGetProperty("appPassword", out var pwdNode) && !string.IsNullOrWhiteSpace(pwdNode.GetString()))
                        {
                            password = pwdNode.GetString()!;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse global_system_rules for email config: {ex.Message}");
                }
            }

            // If SMTP is not configured, we just log it (Mock mode)
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogInformation("================================================");
                _logger.LogInformation($"[MOCK EMAIL] To: {to}");
                _logger.LogInformation($"[MOCK EMAIL] Subject: {subject}");
                _logger.LogInformation($"[MOCK EMAIL] Body: {body}");
                _logger.LogInformation("================================================");
                System.IO.File.AppendAllText("email_log.txt", $"[EmailService] Mock Email hit. Missing host/username/password. Host='{host}' User='{username}' Pass='{password}'. To: {to}\n");
                return;
            }

            try
            {
                int.TryParse(portString, out int port);
                if (port == 0) port = 587; // default SMTP port

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true
                };

                // Handle multiple comma-separated emails
                var emails = to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "Apple Esports System"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                // Handle multiple comma-separated emails
                foreach (var email in emails)
                {
                    mailMessage.To.Add(email.Trim());
                }

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email successfully sent to {to} with subject: {subject}");
                System.IO.File.AppendAllText("email_log.txt", $"[EmailService] SUCCESS sending email to {to} with subject {subject}\n");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email to {to}: {ex.Message}");
                System.IO.File.AppendAllText("email_log.txt", $"[EmailService] EXCEPTION SENDING: {ex.ToString()}\n");
            }
        }
    }
}
