using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Interfaces;
using Core.Dtos;
using Core.Enums;
using Api.Models;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : BaseApiController
    {
        private readonly IFileService _fileService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IFileService fileService, ISubscriptionService subscriptionService, ILogger<FilesController> logger)
        {
            _fileService = fileService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string? description = null)
        {
            var userId = 0; // Initialize to avoid scope issues
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out userId);
                if (authError != null) return authError;

                // Validate file upload
                if (file == null || file.Length == 0)
                    return Error("Please select a file to upload.", 400, "NO_FILE");

                if (file.Length > 50 * 1024 * 1024) // 50MB limit
                    return Error("File size cannot exceed 50MB.", 400, "FILE_TOO_LARGE");

                // Check if user has File Sharing subscription
                var hasFileSharing = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.FileSharing);
                if (!hasFileSharing)
                {
                    return Error("File sharing feature requires an active subscription.", 403, "SUBSCRIPTION_REQUIRED", 
                        new List<string> { "Please subscribe to the File Sharing plan to upload files." });
                }

                // Determine file type based on extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileType = extension switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => FileType.Image,
                    ".mp3" or ".wav" or ".ogg" or ".m4a" => FileType.Audio,
                    ".mp4" => FileType.Video,
                    ".pdf" or ".doc" or ".docx" or ".txt" => FileType.Document,
                    _ => FileType.Other
                };

                _logger.LogInformation("File upload validation - Name: {FileName}, Size: {FileSize}, ContentType: {ContentType}, DetectedType: {FileType}",
                    file.FileName, file.Length, file.ContentType, fileType);

                var isValid = await _fileService.ValidateFileAsync(file, fileType);
                if (!isValid)
                {
                    _logger.LogWarning("File validation failed - Name: {FileName}, Size: {FileSize}, Type: {FileType}",
                        file.FileName, file.Length, fileType);
                    return Error("Invalid file type or size. Please check our supported file formats.", 400, "INVALID_FILE");
                }

                // Convert IFormFile to byte array
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                var uploadRequest = new FileUploadRequest
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    FileData = fileData,
                    UserId = userId,
                    FileType = fileType
                };

                var result = await _fileService.UploadFileAsync(uploadRequest, userId);

                var responseData = new
                {
                    Url = result.FileUrl,
                    FileName = result.FileName,
                    Size = result.FileSize,
                    ContentType = result.ContentType,
                    UploadedAt = result.UploadedAt,
                    Description = description
                };

                return Success(responseData, "File uploaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file for user {UserId}", userId);
                return HandleException(ex, "Failed to upload file");
            }
        }
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string fileUrl)
        {
            var userId = 0;
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out userId);
                if (authError != null) return authError;

                // Validate file URL
                var validationError = ValidateRequired((fileUrl, "fileUrl"));
                if (validationError != null) return validationError;

                // Check if user has File Sharing subscription
                var hasFileSharing = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.FileSharing);
                if (!hasFileSharing)
                {
                    return Error("File sharing feature requires an active subscription.", 403, "SUBSCRIPTION_REQUIRED");
                }

                var fileData = await _fileService.DownloadFileAsync(fileUrl, userId);
                if (fileData == null || fileData.Length == 0)
                {
                    return Error("File not found or access denied.", 404, "FILE_NOT_FOUND");
                }

                var fileName = Path.GetFileName(fileUrl) ?? "downloaded_file";
                return File(fileData, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException)
            {
                return Error("The requested file was not found.", 404, "FILE_NOT_FOUND");
            }
            catch (UnauthorizedAccessException)
            {
                return Error("You don't have permission to access this file.", 403, "ACCESS_DENIED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileUrl} for user {UserId}", fileUrl, userId);
                return HandleException(ex, "Failed to download file");
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteFile([FromQuery] string fileUrl)
        {
            var userId = 0;
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out userId);
                if (authError != null) return authError;

                // Validate file URL
                var validationError = ValidateRequired((fileUrl, "fileUrl"));
                if (validationError != null) return validationError;

                // Check if user has File Sharing subscription
                var hasFileSharing = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.FileSharing);
                if (!hasFileSharing)
                {
                    return Error("File sharing feature requires an active subscription.", 403, "SUBSCRIPTION_REQUIRED");
                }

                var deleted = await _fileService.DeleteFileAsync(fileUrl, userId);
                if (!deleted)
                {
                    return Error("File not found or you don't have permission to delete it.", 404, "FILE_NOT_FOUND");
                }

                return Success("File deleted successfully.");
            }
            catch (UnauthorizedAccessException)
            {
                return Error("You don't have permission to delete this file.", 403, "ACCESS_DENIED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileUrl} for user {UserId}", fileUrl, userId);
                return HandleException(ex, "Failed to delete file");
            }
        }
    }
}