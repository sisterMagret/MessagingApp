using Core.Contracts;
using Core.Dtos;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService) => _authService = authService;

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            try
            {
                // Validate request
                var validationError = ValidateRequired(
                    (request?.Email, "Email"),
                    (request?.Password, "Password")
                );
                if (validationError != null) return validationError;

                // Validate email format
                if (!IsValidEmail(request!.Email))
                    return Error("Please provide a valid email address.", 400, "INVALID_EMAIL");

                // Validate password strength
                if (request.Password.Length < 6)
                    return Error("Password must be at least 6 characters long.", 400, "WEAK_PASSWORD");

                var result = await _authService.RegisterAsync(request);
                return Success(result, "User registered successfully. You can now log in.");
            }
            catch (Exception ex) when (ex.Message.Contains("Email already exists"))
            {
                return Error("An account with this email address already exists.", 409, "EMAIL_EXISTS");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to register user");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            try
            {
                // Validate request
                var validationError = ValidateRequired(
                    (request?.Email, "Email"),
                    (request?.Password, "Password")
                );
                if (validationError != null) return validationError;

                var result = await _authService.LoginAsync(request!);
                return Success(result, "Login successful.");
            }
            catch (UnauthorizedAccessException)
            {
                return Error("Invalid email or password.", 401, "INVALID_CREDENTIALS");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to authenticate user");
            }
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            try
            {
                var authError = ValidateUserAuth(out var userId);
                if (authError != null) return authError;

                var userEmail = GetCurrentUserEmail();
                var userData = new
                {
                    UserId = userId,
                    Email = userEmail,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                };

                return Success(userData, "User information retrieved successfully.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to retrieve user information");
            }
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> SearchUser([FromQuery] string email)
        {
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out _);
                if (authError != null) return authError;

                // Validate email parameter
                var validationError = ValidateRequired((email, "email"));
                if (validationError != null) return validationError;

                if (!IsValidEmail(email))
                    return Error("Please provide a valid email address.", 400, "INVALID_EMAIL");

                var user = await _authService.FindUserByEmailAsync(email);
                if (user == null)
                {
                    return Error("No user found with the specified email address.", 404, "USER_NOT_FOUND");
                }

                var userData = new { UserId = user.Id, user.Email };
                return Success(userData, "User found successfully.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to search for user");
            }
        }
    }
}
