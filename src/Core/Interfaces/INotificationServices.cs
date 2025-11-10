namespace Core.Interfaces
{
    public interface INotificationService
    {
        Task NotifyUserAsync(string userId, string message);
    }
}
