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
        var smtpServer = _configuration["SMTP_SERVER"] ?? string.Empty;
        var portStr = _configuration["SMTP_PORT"] ?? string.Empty;
        var senderName = _configuration["SMTP_SENDER_NAME"];
        var senderEmail = _configuration["SMTP_SENDER_EMAIL"] ?? string.Empty;
        var username = _configuration["SMTP_USER_NAME"];
        var password = _configuration["SMTP_PASSWORD"] ?? string.Empty;

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

            await smtp.SendAsync(new SendRequest(email));
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