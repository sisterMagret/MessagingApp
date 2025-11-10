namespace Core.Contracts
{
    using System.Threading.Tasks;

    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string body);

    }
}
