using sandbox_api.Data;
using sandbox_api.Models;
using sandbox_api.Utils;

namespace sandbox_api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly SimulatedDatabase _db;

        public UserRepository(SimulatedDatabase db)
        {
            _db = db;
        }

        public async Task<Result<List<User>, DatabaseError>> GetAllUsersAsync()
        {
            return await ResultHelpers.TryAsync(
                async () => await _db.GetAllUsersAsync(),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve users", ex)
            );
        }

        public async Task<Result<User, DatabaseError>> GetUserByIdAsync(int id)
        {
            var result = await ResultHelpers.TryAsync(
                async () => await _db.GetUserByIdAsync(id),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve user", ex)
            );

            return result.Bind(user =>
                user != null
                    ? Result<User, DatabaseError>.Success(user)
                    : new NotFoundError("User", id.ToString())
            );
        }

        public async Task<Result<User, DatabaseError>> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new ValidationError("Email", "Email cannot be empty");
            }

            var result = await ResultHelpers.TryAsync(
                async () => await _db.GetUserByEmailAsync(email),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve user", ex)
            );

            return result.Bind(user =>
                user != null
                    ? Result<User, DatabaseError>.Success(user)
                    : new NotFoundError("User", email)
            );
        }

        public async Task<Result<User, DatabaseError>> CreateUserAsync(string username, string email, string fullName)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(username))
                return new ValidationError("Username", "Username cannot be empty");

            if (string.IsNullOrWhiteSpace(email))
                return new ValidationError("Email", "Email cannot be empty");

            if (string.IsNullOrWhiteSpace(fullName))
                return new ValidationError("FullName", "Full name cannot be empty");

            if (!email.Contains('@'))
                return new ValidationError("Email", "Invalid email format");

            // Check for duplicate email
            var existingByEmail = await _db.GetUserByEmailAsync(email);
            if (existingByEmail != null)
                return new DuplicateError("Email", email);

            // Check for duplicate username
            var existingByUsername = await _db.GetUserByUsernameAsync(username);
            if (existingByUsername != null)
                return new DuplicateError("Username", username);

            // Create user
            var newUser = new User
            {
                Username = username,
                Email = email,
                FullName = fullName,
                IsActive = true
            };

            return await ResultHelpers.TryAsync(
                async () => await _db.AddUserAsync(newUser),
                ex => new DatabaseError("DB_ERROR", "Failed to create user", ex)
            );
        }

        public async Task<Result<User, DatabaseError>> UpdateUserAsync(int id, string? username, string? email, string? fullName, bool? isActive)
        {
            // Get existing user
            var getUserResult = await GetUserByIdAsync(id);
            if (getUserResult.IsFailure)
                return getUserResult.Error;

            var user = getUserResult.Value;

            // Update only provided fields
            if (!string.IsNullOrWhiteSpace(username))
            {
                // Check for duplicate username
                var existingByUsername = await _db.GetUserByUsernameAsync(username);
                if (existingByUsername != null && existingByUsername.Id != id)
                    return new DuplicateError("Username", username);

                user.Username = username;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                if (!email.Contains('@'))
                    return new ValidationError("Email", "Invalid email format");

                // Check for duplicate email
                var existingByEmail = await _db.GetUserByEmailAsync(email);
                if (existingByEmail != null && existingByEmail.Id != id)
                    return new DuplicateError("Email", email);

                user.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                user.FullName = fullName;
            }

            if (isActive.HasValue)
            {
                user.IsActive = isActive.Value;
            }

            var updateResult = await ResultHelpers.TryAsync(
                async () => await _db.UpdateUserAsync(user),
                ex => new DatabaseError("DB_ERROR", "Failed to update user", ex)
            );

            return updateResult.Bind(success =>
                success
                    ? Result<User, DatabaseError>.Success(user)
                    : new DatabaseError("UPDATE_FAILED", "Failed to update user")
            );
        }

        public async Task<Result<Utils.Unit, DatabaseError>> DeleteUserAsync(int id)
        {
            // Check if user exists
            var getUserResult = await GetUserByIdAsync(id);
            if (getUserResult.IsFailure)
                return getUserResult.Error;

            var deleteResult = await ResultHelpers.TryAsync(
                async () => await _db.DeleteUserAsync(id),
                ex => new DatabaseError("DB_ERROR", "Failed to delete user", ex)
            );

            return deleteResult.Bind(success =>
                success
                    ? Result<Utils.Unit, DatabaseError>.Success(Utils.Unit.Value)
                    : new DatabaseError("DELETE_FAILED", "Failed to delete user")
            );
        }

        public async Task<Result<User, DatabaseError>> RecordLoginAsync(int id)
        {
            var getUserResult = await GetUserByIdAsync(id);
            if (getUserResult.IsFailure)
                return getUserResult.Error;

            var user = getUserResult.Value;

            if (!user.IsActive)
                return new InvalidOperationError("Login", "User account is inactive");

            user.LastLoginAt = DateTime.UtcNow;

            var updateResult = await ResultHelpers.TryAsync(
                async () => await _db.UpdateUserAsync(user),
                ex => new DatabaseError("DB_ERROR", "Failed to record login", ex)
            );

            return updateResult.Bind(success =>
                success
                    ? Result<User, DatabaseError>.Success(user)
                    : new DatabaseError("UPDATE_FAILED", "Failed to record login")
            );
        }
    }
}
