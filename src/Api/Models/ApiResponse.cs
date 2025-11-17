namespace Api.Models
{
    /// <summary>
    /// Standardized API response model for consistent error and success responses
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse<T> SuccessResponse(T data, string message = "Operation completed successfully")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResponse(string message, string? errorCode = null, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Errors = errors ?? new List<string>()
            };
        }
    }

    /// <summary>
    /// Simple API response without data payload
    /// </summary>
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public string? ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse SuccessResponse(string message = "Operation completed successfully")
        {
            return new ApiResponse
            {
                Success = true,
                Message = message
            };
        }

        public static ApiResponse ErrorResponse(string message, string? errorCode = null, List<string>? errors = null)
        {
            return new ApiResponse
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Errors = errors ?? new List<string>()
            };
        }
    }
}