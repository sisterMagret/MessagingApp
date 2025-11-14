using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Interfaces;
using Core.Dtos;
using Core.Enums;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
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
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Check if user has File Sharing subscription
            var hasFileSharing = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.FileSharing);
            if (!hasFileSharing)
            {
                return StatusCode(403, "File sharing feature requires a subscription. Please subscribe to the File Sharing plan.");
            }

            try
            {
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

                // Debug logging
                _logger.LogInformation("File upload validation - Name: {FileName}, Size: {FileSize}, ContentType: {ContentType}, DetectedType: {FileType}",
                    file.FileName, file.Length, file.ContentType, fileType);

                // Validate file
                var isValid = await _fileService.ValidateFileAsync(file, fileType);
                if (!isValid)
                {
                    _logger.LogWarning("File validation failed - Name: {FileName}, Size: {FileSize}, Type: {FileType}",
                        file.FileName, file.Length, fileType);
                    return BadRequest("Invalid file type or size.");
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

                return Ok(new
                {
                    url = result.FileUrl,
                    filename = result.FileName,
                    size = result.FileSize,
                    contentType = result.ContentType,
                    uploadedAt = result.UploadedAt,
                    message = "File uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file for user {UserId}", userId);
                return StatusCode(500, "An error occurred while uploading the file.");
            }
        }
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
            {
                return BadRequest("File URL is required.");
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Check if user has File Sharing subscription
            var hasFileSharing = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.FileSharing);
            if (!hasFileSharing)
            {
                return StatusCode(403, "File sharing feature requires a subscription. Please subscribe to the File Sharing plan.");
            }

            try
            {
                var fileData = await _fileService.DownloadFileAsync(fileUrl, userId);
                if (fileData == null || fileData.Length == 0)
                {
                    return NotFound("File not found.");
                }

                // Extract filename from URL or use a default
                var fileName = Path.GetFileName(fileUrl) ?? "downloaded_file";

                return File(fileData, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileUrl} for user {UserId}", fileUrl, userId);
                return StatusCode(500, "An error occurred while downloading the file.");
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteFile([FromQuery] string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
            {
                return BadRequest("File URL is required.");
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Check if user has File Sharing subscription
            var hasFileSharing = await _subscriptionService.HasActiveFeatureAsync(userId, FeatureType.FileSharing);
            if (!hasFileSharing)
            {
                return StatusCode(403, "File sharing feature requires a subscription. Please subscribe to the File Sharing plan.");
            }

            try
            {
                var deleted = await _fileService.DeleteFileAsync(fileUrl, userId);
                if (!deleted)
                {
                    return NotFound("File not found or access denied.");
                }

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileUrl} for user {UserId}", fileUrl, userId);
                return StatusCode(500, "An error occurred while deleting the file.");
            }
        }
    }
}