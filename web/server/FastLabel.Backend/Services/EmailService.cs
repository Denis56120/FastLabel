namespace FastLabel.Backend.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}

public class EmailService(IConfiguration configuration) : IEmailService
{
    private readonly IConfiguration _configuration = configuration;

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var smtpServer = _configuration["SMTP_SERVER"];
        var portStr = _configuration["SMTP_PORT"];
        var senderName = _configuration["SMTP_SENDER_NAME"];
        var senderEmail = _configuration["SMTP_SENDER_EMAIL"];
        var username = _configuration["SMTP_USER_NAME"];
        var password = _configuration["SMTP_PASSWORD"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(smtpServer))
        {
            var ex = new InvalidOperationException("smtp.error.server_missing");
            AppLog.Error(ex);
            throw ex;
        }

        if (string.IsNullOrWhiteSpace(portStr) || !int.TryParse(portStr, out int port) || port is < 1 or > 65535)
        {
            var ex = new InvalidOperationException("smtp.error.port_invalid");
            AppLog.Error(ex);
            throw ex;
        }

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            var ex = new InvalidOperationException("smtp.error.sender_missing");
            AppLog.Error(ex);
            throw ex;
        }

        if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(password))
        {
            var ex = new InvalidOperationException("smtp.error.username_missing");
            AppLog.Error(ex);
            throw ex;
        }

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(senderName, senderEmail));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        email.Body = new TextPart(TextFormat.Plain)
        {
            Text = message
        };

        var secureSocketOptions = port == 465
            ? SecureSocketOptions.SslOnConnect
            : port == 587
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

        using var smtp = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            await smtp.ConnectAsync(smtpServer, port, secureSocketOptions);

            if (!string.IsNullOrEmpty(username))
            {
                await smtp.AuthenticateAsync(username, password);
            }

            await smtp.SendAsync(email);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            throw new InvalidOperationException("admin.smtp.error.send_failed", ex);
        }
        finally
        {
            if (smtp.IsConnected)
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}