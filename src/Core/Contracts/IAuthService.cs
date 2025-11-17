using Core.Dtos;
using Core.Entities;

namespace Core.Contracts

{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<User?> FindUserByEmailAsync(string email);
    }
}
