using Core.Dtos;
using Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private readonly string[] _allowedImageTypes = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        private readonly string[] _allowedDocumentTypes = { ".pdf", ".doc", ".docx", ".txt" };
        private readonly string[] _allowedAudioTypes = { ".mp3", ".wav", ".ogg", ".m4a" };
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<FileUploadResponse> UploadFileAsync(FileUploadRequest request, int userId)
        {
            if (!await ValidateFileAsync(CreateFormFile(request), request.FileType))
                throw new ArgumentException("Invalid file");

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", userId.ToString());
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{request.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await File.WriteAllBytesAsync(filePath, request.FileData);

            var fileUrl = $"/uploads/{userId}/{fileName}";

            _logger.LogInformation("User {UserId} uploaded file: {FileName}", userId, fileName);

            return new FileUploadResponse
            {
                FileUrl = fileUrl,
                FileName = request.FileName,
                FileSize = request.FileSize,
                ContentType = request.ContentType,
                UploadedAt = DateTime.UtcNow
            };
        }

        public async Task<bool> DeleteFileAsync(string fileUrl, int userId)
        {
            try
            {
                var fileName = Path.GetFileName(fileUrl);
                var filePath = Path.Combine(_environment.WebRootPath, "uploads", userId.ToString(), fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("User {UserId} deleted file: {FileName}", userId, fileName);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileUrl} for user {UserId}", fileUrl, userId);
                return false;
            }
        }

        public async Task<byte[]> DownloadFileAsync(string fileUrl, int userId)
        {
            var fileName = Path.GetFileName(fileUrl);
            var filePath = Path.Combine(_environment.WebRootPath, "uploads", userId.ToString(), fileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found");

            return await File.ReadAllBytesAsync(filePath);
        }

        public async Task<bool> ValidateFileAsync(IFormFile file, FileType fileType)
        {
            if (file == null || file.Length == 0)
                return false;

            if (file.Length > _maxFileSize)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return fileType switch
            {
                FileType.Image => _allowedImageTypes.Contains(extension),
                FileType.Document => _allowedDocumentTypes.Contains(extension),
                FileType.Audio => _allowedAudioTypes.Contains(extension),
                FileType.Video => extension == ".mp4",
                FileType.Other => true,
                _ => false
            };
        }

        // Helper method to convert byte array to IFormFile for validation
        private IFormFile CreateFormFile(FileUploadRequest request)
        {
            var stream = new MemoryStream(request.FileData);
            return new FormFile(stream, 0, request.FileSize, "file", request.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = request.ContentType
            };
        }
    }
}