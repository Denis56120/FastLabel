using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace FastLabel.Backend.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string message);
}

public class EmailService(IConfiguration configuration) : IEmailService
{
    private readonly IConfiguration _configuration = configuration;

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var smtpServer = _configuration["SmtpSettings:Server"];
        var portStr = _configuration["SmtpSettings:Port"];
        var senderName = _configuration["SmtpSettings:SenderName"] ?? "FastLabel";
        var senderEmail = _configuration["SmtpSettings:SenderEmail"];
        var username = _configuration["SmtpSettings:Username"];
        var password = _configuration["SmtpSettings:Password"] ?? string.Empty;

        if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(portStr) || string.IsNullOrEmpty(senderEmail))
        {
            throw new InvalidOperationException("Les paramètres SMTP ne sont pas correctement configurés dans appsettings.json.");
        }

        int port = int.Parse(portStr);

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(senderName, senderEmail));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        email.Body = new TextPart(TextFormat.Plain)
        {
            Text = message
        };

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(smtpServer, port, SecureSocketOptions.Auto);

            if (!string.IsNullOrEmpty(username))
            {
                await smtp.AuthenticateAsync(username, password);
            }

            await smtp.SendAsync(new SmtpSendRequest(email));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Échec de l'envoi de l'e-mail à {toEmail}: {ex.Message}", ex);
        }
        finally
        {
            await smtp.DisconnectAsync(true);
        }
    }
}