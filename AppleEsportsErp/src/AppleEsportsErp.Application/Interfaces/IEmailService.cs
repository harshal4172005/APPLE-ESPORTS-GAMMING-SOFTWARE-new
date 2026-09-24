using System.Threading.Tasks;

namespace AppleEsportsErp.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);

        /// <summary>
        /// Sends immediately and reports what actually happened, instead of the silent
        /// swallow-and-log-to-a-file behaviour <see cref="SendEmailAsync"/> uses for real
        /// production mail (which must never let a bad SMTP password block a password reset
        /// or a top-up receipt from at least being attempted). This is the only way an admin
        /// can find out *why* mail isn't arriving without going to read a log file on the server.
        /// </summary>
        Task<(bool Success, string Message)> SendTestEmailAsync(string toAddress);
    }
}
