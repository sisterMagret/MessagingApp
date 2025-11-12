using Core.Enums;

namespace Core.Dtos
{
    public class FileUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        public int UserId { get; set; }
        public FileType FileType { get; set; }
    }

    public class FileUploadResponse
    {
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}