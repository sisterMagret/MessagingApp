using Core.Dtos;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Services
{
    public class FileServiceTests : ServiceTestBase
    {
        private readonly IFileService _fileService;
        private readonly Mock<ILogger<FileService>> _mockLogger;
        private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;

        public FileServiceTests()
        {
            _mockLogger = new Mock<ILogger<FileService>>();
            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();

            // Set up web root path for testing
            _mockWebHostEnvironment.Setup(e => e.WebRootPath).Returns("/tmp/test-uploads");

            _fileService = new FileService(_mockWebHostEnvironment.Object, _mockLogger.Object);
        }

        private Mock<IFormFile> CreateMockFile(string fileName, string contentType, long size, byte[] content)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(size);
            
            var stream = new MemoryStream(content);
            mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken token) => {
                    stream.Position = 0;
                    return stream.CopyToAsync(target, token);
                });

            return mockFile;
        }

        [Fact]
        public async Task ValidateFileAsync_WithValidImageFile_ShouldReturnTrue()
        {
            // Arrange
            var content = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG header
            var mockFile = CreateMockFile("test.jpg", "image/jpeg", 1024, content);

            // Act
            var result = await _fileService.ValidateFileAsync(mockFile.Object, FileType.Image);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateFileAsync_WithOversizedFile_ShouldReturnFalse()
        {
            // Arrange
            var content = new byte[1024 * 1024 * 15]; // 15MB file (over 10MB limit)
            var mockFile = CreateMockFile("large.jpg", "image/jpeg", content.Length, content);

            // Act
            var result = await _fileService.ValidateFileAsync(mockFile.Object, FileType.Image);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateFileAsync_WithInvalidFileType_ShouldReturnFalse()
        {
            // Arrange
            var content = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP header
            var mockFile = CreateMockFile("test.exe", "application/octet-stream", 1024, content);

            // Act
            var result = await _fileService.ValidateFileAsync(mockFile.Object, FileType.Image);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("test.jpg", "image/jpeg", FileType.Image)]
        [InlineData("test.png", "image/png", FileType.Image)]
        [InlineData("test.gif", "image/gif", FileType.Image)]
        [InlineData("audio.mp3", "audio/mpeg", FileType.Audio)]
        [InlineData("audio.wav", "audio/wav", FileType.Audio)]
        [InlineData("video.mp4", "video/mp4", FileType.Video)]
        [InlineData("document.pdf", "application/pdf", FileType.Document)]
        [InlineData("document.txt", "text/plain", FileType.Document)]
        public async Task ValidateFileAsync_WithValidFileTypes_ShouldReturnTrue(string fileName, string contentType, FileType fileType)
        {
            // Arrange
            var content = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var mockFile = CreateMockFile(fileName, contentType, 1024, content);

            // Act
            var result = await _fileService.ValidateFileAsync(mockFile.Object, fileType);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("test.exe", "application/octet-stream", FileType.Image)]
        [InlineData("script.js", "text/javascript", FileType.Document)]
        [InlineData("virus.bat", "application/batch", FileType.Other)]
        public async Task ValidateFileAsync_WithInvalidFileTypes_ShouldReturnFalse(string fileName, string contentType, FileType fileType)
        {
            // Arrange
            var content = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var mockFile = CreateMockFile(fileName, contentType, 1024, content);

            // Act
            var result = await _fileService.ValidateFileAsync(mockFile.Object, fileType);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UploadFileAsync_WithValidFile_ShouldReturnFileUploadResponse()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var content = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG header
            var uploadRequest = new FileUploadRequest
            {
                FileName = "test.jpg",
                ContentType = "image/jpeg",
                FileSize = content.Length,
                FileData = content,
                UserId = user.Id,
                FileType = FileType.Image
            };

            // Act
            var result = await _fileService.UploadFileAsync(uploadRequest, user.Id);

            // Assert
            result.Should().NotBeNull();
            result.FileName.Should().Be("test.jpg");
            result.ContentType.Should().Be("image/jpeg");
            result.FileSize.Should().Be(content.Length);
            result.FileUrl.Should().NotBeNullOrEmpty();
            result.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task UploadFileAsync_WithNullRequest_ShouldThrowArgumentNullException()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act & Assert
            var act = async () => await _fileService.UploadFileAsync(null!, user.Id);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task UploadFileAsync_WithEmptyFileName_ShouldThrowArgumentException()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var content = new byte[] { 0xFF, 0xD8, 0xFF };
            var uploadRequest = new FileUploadRequest
            {
                FileName = "",
                ContentType = "image/jpeg",
                FileSize = content.Length,
                FileData = content,
                UserId = user.Id,
                FileType = FileType.Image
            };

            // Act & Assert
            var act = async () => await _fileService.UploadFileAsync(uploadRequest, user.Id);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task UploadFileAsync_WithZeroSizeFile_ShouldThrowArgumentException()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var uploadRequest = new FileUploadRequest
            {
                FileName = "test.jpg",
                ContentType = "image/jpeg",
                FileSize = 0,
                FileData = Array.Empty<byte>(),
                UserId = user.Id,
                FileType = FileType.Image
            };

            // Act & Assert
            var act = async () => await _fileService.UploadFileAsync(uploadRequest, user.Id);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task DownloadFileAsync_WithValidFileUrl_ShouldReturnFileData()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            var uploadRequest = new FileUploadRequest
            {
                FileName = "download-test.jpg",
                ContentType = "image/jpeg",
                FileSize = content.Length,
                FileData = content,
                UserId = user.Id,
                FileType = FileType.Image
            };

            // Upload file first
            var uploadResult = await _fileService.UploadFileAsync(uploadRequest, user.Id);

            // Act
            var downloadResult = await _fileService.DownloadFileAsync(uploadResult.FileUrl, user.Id);

            // Assert
            downloadResult.Should().NotBeNull();
            downloadResult.Should().BeEquivalentTo(content);
        }

        [Fact]
        public async Task DownloadFileAsync_WithInvalidFileUrl_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act & Assert
            var act = async () => await _fileService.DownloadFileAsync("invalid-file-url.jpg", user.Id);
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task DownloadFileAsync_WithEmptyFileUrl_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act & Assert
            var act = async () => await _fileService.DownloadFileAsync("", user.Id);
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task DeleteFileAsync_WithValidFileUrl_ShouldReturnTrue()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            var uploadRequest = new FileUploadRequest
            {
                FileName = "delete-test.jpg",
                ContentType = "image/jpeg",
                FileSize = content.Length,
                FileData = content,
                UserId = user.Id,
                FileType = FileType.Image
            };

            // Upload file first
            var uploadResult = await _fileService.UploadFileAsync(uploadRequest, user.Id);

            // Act
            var deleteResult = await _fileService.DeleteFileAsync(uploadResult.FileUrl, user.Id);

            // Assert
            deleteResult.Should().BeTrue();

            // Verify file is deleted
            var act = async () => await _fileService.DownloadFileAsync(uploadResult.FileUrl, user.Id);
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task DeleteFileAsync_WithInvalidFileUrl_ShouldReturnFalse()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act
            var result = await _fileService.DeleteFileAsync("invalid-file-url.jpg", user.Id);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteFileAsync_WithEmptyFileUrl_ShouldReturnFalse()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act
            var result = await _fileService.DeleteFileAsync("", user.Id);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task FileLifecycle_UploadDownloadDelete_ShouldWorkCorrectly()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var originalContent = System.Text.Encoding.UTF8.GetBytes("This is test file content for lifecycle testing.");
            var uploadRequest = new FileUploadRequest
            {
                FileName = "lifecycle-test.txt",
                ContentType = "text/plain",
                FileSize = originalContent.Length,
                FileData = originalContent,
                UserId = user.Id,
                FileType = FileType.Document
            };

            // Act & Assert - Upload
            var uploadResult = await _fileService.UploadFileAsync(uploadRequest, user.Id);
            uploadResult.Should().NotBeNull();
            uploadResult.FileUrl.Should().NotBeNullOrEmpty();

            // Act & Assert - Download
            var downloadedContent = await _fileService.DownloadFileAsync(uploadResult.FileUrl, user.Id);
            downloadedContent.Should().NotBeNull();
            downloadedContent.Should().BeEquivalentTo(originalContent);

            // Act & Assert - Delete
            var deleteResult = await _fileService.DeleteFileAsync(uploadResult.FileUrl, user.Id);
            deleteResult.Should().BeTrue();

            // Act & Assert - Verify deletion
            var act = async () => await _fileService.DownloadFileAsync(uploadResult.FileUrl, user.Id);
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task MultipleFiles_ConcurrentOperations_ShouldWorkCorrectly()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var files = new List<FileUploadRequest>();

            for (int i = 0; i < 5; i++)
            {
                var content = System.Text.Encoding.UTF8.GetBytes($"Content for file {i}");
                files.Add(new FileUploadRequest
                {
                    FileName = $"concurrent-test-{i}.txt",
                    ContentType = "text/plain",
                    FileSize = content.Length,
                    FileData = content,
                    UserId = user.Id,
                    FileType = FileType.Document
                });
            }

            // Act - Upload all files concurrently
            var uploadTasks = files.Select(file => _fileService.UploadFileAsync(file, user.Id));
            var uploadResults = await Task.WhenAll(uploadTasks);

            // Assert - All uploads should succeed
            uploadResults.Should().HaveCount(5);
            uploadResults.Should().AllSatisfy(result => 
            {
                result.Should().NotBeNull();
                result.FileUrl.Should().NotBeNullOrEmpty();
            });

            // Act - Download all files concurrently
            var downloadTasks = uploadResults.Select(result => _fileService.DownloadFileAsync(result.FileUrl, user.Id));
            var downloadResults = await Task.WhenAll(downloadTasks);

            // Assert - All downloads should succeed
            downloadResults.Should().HaveCount(5);
            downloadResults.Should().AllSatisfy(content => content.Should().NotBeNull());

            // Act - Delete all files concurrently
            var deleteTasks = uploadResults.Select(result => _fileService.DeleteFileAsync(result.FileUrl, user.Id));
            var deleteResults = await Task.WhenAll(deleteTasks);

            // Assert - All deletions should succeed
            deleteResults.Should().HaveCount(5);
            deleteResults.Should().AllSatisfy(result => result.Should().BeTrue());
        }

        [Theory]
        [InlineData(FileType.Image, "test.jpg", "image/jpeg")]
        [InlineData(FileType.Audio, "test.mp3", "audio/mpeg")]
        [InlineData(FileType.Video, "test.mp4", "video/mp4")]
        [InlineData(FileType.Document, "test.pdf", "application/pdf")]
        [InlineData(FileType.Other, "test.zip", "application/zip")]
        public async Task UploadFileAsync_WithDifferentFileTypes_ShouldHandleCorrectly(FileType fileType, string fileName, string contentType)
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var content = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var uploadRequest = new FileUploadRequest
            {
                FileName = fileName,
                ContentType = contentType,
                FileSize = content.Length,
                FileData = content,
                UserId = user.Id,
                FileType = fileType
            };

            // Act
            var result = await _fileService.UploadFileAsync(uploadRequest, user.Id);

            // Assert
            result.Should().NotBeNull();
            result.FileName.Should().Be(fileName);
            result.ContentType.Should().Be(contentType);
            result.FileSize.Should().Be(content.Length);

            // Clean up
            await _fileService.DeleteFileAsync(result.FileUrl, user.Id);
        }

        [Fact]
        public async Task UserIsolation_ShouldPreventCrossUserAccess()
        {
            // Arrange
            var user1 = await CreateTestUserAsync("user1@test.com");
            var user2 = await CreateTestUserAsync("user2@test.com");
            
            var content = System.Text.Encoding.UTF8.GetBytes("User1's private file");
            var uploadRequest = new FileUploadRequest
            {
                FileName = "private.txt",
                ContentType = "text/plain",
                FileSize = content.Length,
                FileData = content,
                UserId = user1.Id,
                FileType = FileType.Document
            };

            // User1 uploads file
            var uploadResult = await _fileService.UploadFileAsync(uploadRequest, user1.Id);

            // Act & Assert - User2 should not be able to access User1's file
            var user2DownloadAttempt = async () => await _fileService.DownloadFileAsync(uploadResult.FileUrl, user2.Id);
            await user2DownloadAttempt.Should().ThrowAsync<FileNotFoundException>();

            var user2DeleteAttempt = await _fileService.DeleteFileAsync(uploadResult.FileUrl, user2.Id);
            user2DeleteAttempt.Should().BeFalse();

            // Act & Assert - User1 should still have access
            var user1Download = await _fileService.DownloadFileAsync(uploadResult.FileUrl, user1.Id);
            user1Download.Should().NotBeNull();
            user1Download.Should().BeEquivalentTo(content);

            // Clean up
            var deleteResult = await _fileService.DeleteFileAsync(uploadResult.FileUrl, user1.Id);
            deleteResult.Should().BeTrue();
        }
    }
}