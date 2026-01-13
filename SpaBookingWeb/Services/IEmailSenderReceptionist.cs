namespace SpaBookingWeb.Services
{
    public interface IEmailSenderReceptionist
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
