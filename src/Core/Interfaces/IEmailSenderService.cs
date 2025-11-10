public interface IMessageService
{
    Task<object> SendAsync(int senderId, int receiverId, string content);
    Task<IEnumerable<object>> GetInboxAsync(int userId);
}
