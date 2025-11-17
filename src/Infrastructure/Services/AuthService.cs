using Core.Contracts;
using Core.Dtos;
using Core.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly MessagingDbContext _db;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthService(MessagingDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
                throw new Exception("Email already exists.");

            var user = new User
            {
                Email = request.Email,
                PasswordHash = _passwordHasher.HashPassword(null!, request.Password)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return GenerateToken(user);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Invalid credentials.");

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
                throw new UnauthorizedAccessException("Invalid credentials.");

            return GenerateToken(user);
        }

        public async Task<User?> FindUserByEmailAsync(string email)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        private AuthResponse GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddDays(7);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new AuthResponse
            {
                Email = user.Email,
                Token = new JwtSecurityTokenHandler().WriteToken(token)
            };
        }
    }
}
