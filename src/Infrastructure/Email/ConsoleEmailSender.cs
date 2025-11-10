using Core.Interfaces;

namespace Infrastructure.Email;

public class ConsoleEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        Console.WriteLine($"[Email] To: {to}, Subject: {subject}, Body: {body}");
        return Task.CompletedTask;
    }
}
