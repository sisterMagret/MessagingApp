using Core.Dtos;
using Core.Enums;
using Microsoft.AspNetCore.Http;

namespace Core.Interfaces
{
    public interface IFileService
    {
        Task<FileUploadResponse> UploadFileAsync(FileUploadRequest request, int userId);
        Task<bool> DeleteFileAsync(string fileUrl, int userId);
        Task<byte[]> DownloadFileAsync(string fileUrl, int userId);
        Task<bool> ValidateFileAsync(IFormFile file, FileType fileType);
    }
}