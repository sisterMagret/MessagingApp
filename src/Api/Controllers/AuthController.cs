using Core.Contracts;
using Core.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService) => _authService = authService;

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult Me()
        {
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Ok(new { Email = userEmail, UserId = userId, Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList() });
        }

        [HttpGet("search")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> SearchUser([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Email parameter is required");
            }

            var user = await _authService.FindUserByEmailAsync(email);
            if (user == null)
            {
                return NotFound("User not found");
            }

            return Ok(new { UserId = user.Id, Email = user.Email });
        }
    }
}
