namespace Core.Dtos
{
    public class MessageCreateRequest
    {
        public int ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? FileUrl { get; set; }
        public string? VoiceUrl { get; set; }
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string VoiceUrl { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; }
        public int TotalCount { get; }
        public int Page { get; }
        public int PageSize { get; }

        public PagedResult(IEnumerable<T> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }
}
