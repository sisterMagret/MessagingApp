using Core.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface IMessageService
    {
        /// <summary>
        /// Send a message from senderId to receiverId.
        /// Returns the saved message DTO.
        /// </summary>
        Task<MessageDto> SendAsync(int senderId, MessageCreateRequest request);

        /// <summary>
        /// Get inbox (messages received by userId), latest first
        /// </summary>
        Task<IEnumerable<MessageDto>> GetInboxAsync(int userId, int page = 1, int pageSize = 50);

        /// <summary>
        /// Mark message as read.
        /// </summary>
        Task MarkAsReadAsync(int userId, int messageId);
    }
}
