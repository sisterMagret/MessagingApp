public interface IUserService
{
    Task<Core.Entities.User?> RegisterAsync(string email, string password);
    Task<Core.Entities.User?> AuthenticateAsync(string email, string password);
    Task<IEnumerable<Core.Entities.User>> GetUsersToNotifyAsync();
}