using Core.Dtos;

namespace Core.Interfaces
{
    public interface IMessageService
    {
        Task<MessageDto> SendAsync(int senderId, MessageCreateRequest request);
        Task<PagedResult<MessageDto>> GetInboxAsync(int userId, int page, int pageSize);
        Task MarkAsReadAsync(int userId, int messageId);
    }
}
