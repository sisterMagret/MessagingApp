namespace Core.Dtos
{
    public class MessageCreateRequest
    {
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? FileUrl { get; set; }
        public string? VoiceUrl { get; set; }
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? FileUrl { get; set; }
        public string? VoiceUrl { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public PagedResult(List<T> items, int totalCount, int page, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }
}