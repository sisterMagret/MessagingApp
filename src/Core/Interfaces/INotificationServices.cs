namespace Core.Interfaces
{
    public interface INotificationService
    {
        Task NotifyUserAsync(string userId, string message);
        Task NotifyGroupAsync(int groupId, string message, int excludedUserId = 0);
    }
}