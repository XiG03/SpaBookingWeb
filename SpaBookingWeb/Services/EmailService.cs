using MailKit.Net.Smtp;
using MimeKit;

namespace SpaBookingWeb.Services
{


public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_config["EmailSettings:SenderName"], _config["EmailSettings:SenderEmail"]));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        
        var builder = new BodyBuilder();
        builder.HtmlBody = message;
        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        
        await smtp.ConnectAsync(_config["EmailSettings:MailServer"], int.Parse(_config["EmailSettings:MailPort"]), MailKit.Security.SecureSocketOptions.StartTls);
        
        
        await smtp.AuthenticateAsync(_config["EmailSettings:SenderEmail"], _config["EmailSettings:Password"]);
        
        
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}
}
