using System;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Infrastructure.Configuration;

namespace AppleEsportsErp.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly bool _isHeadOffice;

        public EmailService(IUnitOfWork unitOfWork, ILogger<EmailService> logger, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
            _isHeadOffice = configuration.IsHeadOffice();
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to)) return;

            // If this is a branch (not Head Office), queue the email instead of sending it
            if (!_isHeadOffice)
            {
                await QueueEmailForHeadOfficeAsync(to, subject, body);
                return;
            }

            var (host, port, username, password, fromEmail) = await ResolveSmtpConfigAsync();

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
                await SendViaSmtpAsync(host, port, username, password, fromEmail, to, subject, body);
                _logger.LogInformation($"Email successfully sent to {to} with subject: {subject}");
                System.IO.File.AppendAllText("email_log.txt", $"[EmailService] SUCCESS sending email to {to} with subject {subject}\n");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email to {to}: {ex.Message}");
                System.IO.File.AppendAllText("email_log.txt", $"[EmailService] EXCEPTION SENDING: {ex.ToString()}\n");
            }
        }

        public async Task<(bool Success, string Message)> SendTestEmailAsync(string toAddress)
        {
            if (string.IsNullOrWhiteSpace(toAddress))
                return (false, "Enter an address to send the test to first.");

            // A branch has no SMTP connection of its own - SendEmailAsync would just queue this
            // to Head Office instead of actually testing anything, which would sit there
            // looking like nothing happened. Say so directly rather than pretending to test.
            if (!_isHeadOffice)
                return (false, "This machine is a branch, not Head Office - it has no direct email connection to test. Email is only ever sent from Head Office; ask whoever manages that server to test it there.");

            var (host, port, username, password, fromEmail) = await ResolveSmtpConfigAsync();

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "No sender email or app password saved yet. Fill in both fields above, save, then test again.");

            try
            {
                await SendViaSmtpAsync(host, port, username, password, fromEmail, toAddress,
                    "Apple Esports - test email",
                    "This is a test email from Apple Esports System Configuration. If you received this, email notifications, forgot-password links and top-up receipts are working.");

                return (true, $"Sent to {toAddress} - check its inbox (and spam folder).");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(string Host, int Port, string Username, string Password, string FromEmail)> ResolveSmtpConfigAsync()
        {
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

            int.TryParse(portString, out int port);
            if (port == 0) port = 587; // default SMTP port

            return (host, port, username, password, fromEmail);
        }

        private static async Task SendViaSmtpAsync(
            string host, int port, string username, string password, string fromEmail,
            string to, string subject, string body)
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Apple Esports System"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            // Handle multiple comma-separated emails
            foreach (var email in to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                mailMessage.To.Add(email.Trim());
            }

            await client.SendMailAsync(mailMessage);
        }

        private async Task QueueEmailForHeadOfficeAsync(string to, string subject, string body)
        {
            try
            {
                // Get the first branch (this is a branch deployment)
                var branch = await _unitOfWork.Repository<Branch>()
                    .Query()
                    .FirstOrDefaultAsync();

                if (branch == null)
                {
                    _logger.LogWarning("Cannot queue email: no branch found in this deployment");
                    return;
                }

                // Queue email as outbox event so Head Office can send it
                var outboxEntry = new SyncOutboxEntry
                {
                    BranchId = branch.Id,
                    AggregateType = "Email",
                    AggregateId = Guid.NewGuid(),
                    EventType = "email.send_requested",
                    EventData = JsonSerializer.Serialize(new
                    {
                        to,
                        subject,
                        body,
                        requestedAt = DateTime.UtcNow
                    }),
                    CreatedAt = DateTime.UtcNow,
                    SyncedAt = null,
                    AttemptCount = 0
                };

                await _unitOfWork.Repository<SyncOutboxEntry>().AddAsync(outboxEntry);
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation($"Queued email for Head Office to send to {to} with subject: {subject}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to queue email: {ex.Message}");
            }
        }
    }
}
