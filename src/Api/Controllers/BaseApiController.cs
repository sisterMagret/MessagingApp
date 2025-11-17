using Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers
{
    /// <summary>
    /// Base controller with common error handling and utility methods
    /// </summary>
    public class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Get the current user ID from JWT claims
        /// </summary>
        protected int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        /// <summary>
        /// Get the current user email from JWT claims
        /// </summary>
        protected string? GetCurrentUserEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value;
        }

        /// <summary>
        /// Return a standardized success response
        /// </summary>
        protected IActionResult Success<T>(T data, string message = "Operation completed successfully")
        {
            return Ok(ApiResponse<T>.SuccessResponse(data, message));
        }

        /// <summary>
        /// Return a standardized success response without data
        /// </summary>
        protected IActionResult Success(string message = "Operation completed successfully")
        {
            return Ok(ApiResponse.SuccessResponse(message));
        }

        /// <summary>
        /// Return a standardized error response
        /// </summary>
        protected IActionResult Error(string message, int statusCode = 400, string? errorCode = null, List<string>? errors = null)
        {
            var response = ApiResponse.ErrorResponse(message, errorCode, errors);
            return StatusCode(statusCode, response);
        }

        /// <summary>
        /// Handle common exceptions and return appropriate responses
        /// </summary>
        protected IActionResult HandleException(Exception ex, string? customMessage = null)
        {
            var message = customMessage ?? "An error occurred while processing the request.";

            return ex switch
            {
                ArgumentNullException argEx => Error($"Invalid input: {argEx.ParamName} cannot be null.", 400, "INVALID_INPUT"),
                ArgumentException argEx => Error($"Invalid argument: {argEx.Message}", 400, "INVALID_ARGUMENT"),
                UnauthorizedAccessException => Error("You are not authorized to perform this action.", 403, "UNAUTHORIZED"),
                InvalidOperationException invEx => Error($"Invalid operation: {invEx.Message}", 400, "INVALID_OPERATION"),
                KeyNotFoundException => Error("The requested resource was not found.", 404, "NOT_FOUND"),
                TimeoutException => Error("The request timed out. Please try again.", 408, "TIMEOUT"),
                NotImplementedException => Error("This feature is not yet implemented.", 501, "NOT_IMPLEMENTED"),
                _ => Error($"{message} Details: {ex.Message}", 500, "INTERNAL_ERROR")
            };
        }

        /// <summary>
        /// Validate user authentication and return user ID
        /// </summary>
        protected IActionResult? ValidateUserAuth(out int userId)
        {
            userId = 0;
            var userIdValue = GetCurrentUserId();
            
            if (!userIdValue.HasValue)
            {
                return Error("User authentication is required.", 401, "AUTH_REQUIRED");
            }
            
            userId = userIdValue.Value;
            return null; // No error
        }

        /// <summary>
        /// Validate required parameters
        /// </summary>
        protected IActionResult? ValidateRequired(params (object? value, string name)[] parameters)
        {
            var errors = new List<string>();
            
            foreach (var (value, name) in parameters)
            {
                if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
                {
                    errors.Add($"{name} is required.");
                }
            }

            return errors.Count > 0 
                ? Error("Validation failed.", 400, "VALIDATION_ERROR", errors)
                : null;
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        protected bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}