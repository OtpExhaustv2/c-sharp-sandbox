using sandbox_api.Models;
using sandbox_api.Utils;

namespace sandbox_api.Repositories
{
    public interface IUserRepository
    {
        Task<Result<List<User>, DatabaseError>> GetAllUsersAsync();
        Task<Result<User, DatabaseError>> GetUserByIdAsync(int id);
        Task<Result<User, DatabaseError>> GetUserByEmailAsync(string email);
        Task<Result<User, DatabaseError>> CreateUserAsync(string username, string email, string fullName);
        Task<Result<User, DatabaseError>> UpdateUserAsync(int id, string? username, string? email, string? fullName, bool? isActive);
        Task<Result<Utils.Unit, DatabaseError>> DeleteUserAsync(int id);
        Task<Result<User, DatabaseError>> RecordLoginAsync(int id);
    }
}
