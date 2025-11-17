using Core.Contracts;

namespace Infrastructure.Email
{
    public class ConsoleEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            Console.WriteLine($"Email to {to}: {subject} - {body}");
            return Task.CompletedTask;
        }
    }
}
