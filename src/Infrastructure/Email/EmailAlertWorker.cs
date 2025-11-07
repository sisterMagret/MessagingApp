using Core.Interfaces;

namespace Infrastructure.Background;

public class EmailAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailAlertWorker> _logger;

    public EmailAlertWorker(IServiceScopeFactory scopeFactory, ILogger<EmailAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            var offlineUsers = await userService.GetUsersToNotifyAsync();

            foreach (var user in offlineUsers)
            {
                await emailSender.SendAsync(user.Email, "You have unread messages!", "Please check your inbox.");
                _logger.LogInformation($"Sent reminder to {user.Email}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
