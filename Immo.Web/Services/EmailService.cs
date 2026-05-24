using System.Net;
using System.Net.Mail;
using Immo.Data;
using Immo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Immo.Web.Services;

public class EmailService
{
    private readonly ImmoContext _context;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ImmoContext context, ILogger<EmailService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> SendPropertiesEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var settings = await _context.AppSettings.FirstOrDefaultAsync() ?? new AppSettings();

            if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.FromEmail))
            {
                _logger.LogWarning("SMTP settings are not configured. Email not sent to {To}", to);
                return false;
            }

            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.SmtpUseSsl
            };

            if (!string.IsNullOrEmpty(settings.SmtpUsername))
            {
                client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);
            }

            var mail = new MailMessage()
            {
                From = new MailAddress(settings.FromEmail),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            mail.To.Add(to);

            await client.SendMailAsync(mail);
            _logger.LogInformation("Email sent to {To} with subject {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return false;
        }
    }
}
