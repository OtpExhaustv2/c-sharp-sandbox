using Sandbox.utils;

namespace Sandbox.examples
{
    public class ExtensionsExamples
    {
        // ============================================
        // EXAMPLE 1: Tap - Logging without changing result
        // ============================================

        public static Result<User, Error> GetUserWithLogging(int userId)
        {
            return GetUser(userId)
                .Tap(user => Console.WriteLine($"[INFO] Found user: {user.Name}"))
                .TapError(error => Console.WriteLine($"[ERROR] {error.Code}: {error.Message}"));
        }

        // ============================================
        // EXAMPLE 2: Ensure - Validation chains
        // ============================================

        public static Result<User, Error> GetActiveUserWithEmail(int userId)
        {
            return GetUser(userId)
                .Ensure(user => user.Id > 0, user => new Error("INVALID_USER", "User ID must be positive"))
                .Ensure(user => !string.IsNullOrEmpty(user.Email), new Error("NO_EMAIL", "User has no email"))
                .Ensure(user => user.Name.Length >= 3, new Error("INVALID_NAME", "Name too short"));
        }

        // ============================================
        // EXAMPLE 3: OrElse - Fallback strategies
        // ============================================

        public static Result<User, Error> GetUserWithFallback(int userId)
        {
            return GetUserFromCache(userId)
                .OrElse(GetUserFromDatabase(userId))
                .OrElse(err => GetUserFromApi(userId))
                .OrElse(err => GetDefaultUser());
        }

        // ============================================
        // EXAMPLE 4: Recover - Convert error to success
        // ============================================

        public static Result<string, Error> GetUserEmailWithDefault(int userId)
        {
            return GetUser(userId)
                .Map(u => u.Email)
                .Recover(error => "no-reply@example.com");
        }

        // ============================================
        // EXAMPLE 5: Zip - Combining multiple Results
        // ============================================

        public static Result<string, Error> GetUserSummary(int userId)
        {
            var userResult = GetUser(userId);
            var ageResult = GetUserAge(userId);

            return userResult.Zip(ageResult, (user, age) =>
                $"{user.Name} is {age} years old (email: {user.Email})");
        }

        // ============================================
        // EXAMPLE 6: BiMap - Transform both success and error
        // ============================================

        public static Result<string, string> GetUserDisplayInfo(int userId)
        {
            return GetUser(userId)
                .BiMap(
                    user => $"✓ {user.Name} ({user.Email})",
                    error => $"✗ {error.Code}: {error.Message}"
                );
        }

        // ============================================
        // EXAMPLE 7: ToNullable - Converting to nullable
        // ============================================

        public static int? GetUserAgeOrNull(int userId)
        {
            return GetUserAge(userId).ToNullable();
        }

        public static User? GetUserOrNull(int userId)
        {
            return GetUser(userId).ToNullable();
        }

        // ============================================
        // EXAMPLE 8: MapAsync - Async transformation
        // ============================================

        public static async Task<Result<string, Error>> GetUserAvatarUrlAsync(int userId)
        {
            var userResult = GetUser(userId);

            return await userResult.MapAsync(async user =>
            {
                // Simulate async avatar service call
                await Task.Delay(50);
                return $"https://avatars.example.com/{user.Id}.jpg";
            });
        }

        // ============================================
        // EXAMPLE 9: Complex validation pipeline
        // ============================================

        public static Result<User, Error> ValidateUserPipeline(int userId)
        {
            return GetUser(userId)
                .Tap(u => Console.WriteLine($"Validating user {u.Name}..."))
                .Ensure(u => u.Id > 0, new Error("INVALID_ID", "ID must be positive"))
                .Ensure(u => !string.IsNullOrEmpty(u.Email), new Error("NO_EMAIL", "Email required"))
                .Ensure(u => u.Email.Contains("@"), new Error("INVALID_EMAIL", "Invalid email format"))
                .Tap(u => Console.WriteLine($"✓ User {u.Name} validated successfully"))
                .TapError(e => Console.WriteLine($"✗ Validation failed: {e.Message}"));
        }

        // ============================================
        // EXAMPLE 10: Combining Tap with recovery
        // ============================================

        public static Result<User, Error> GetUserSafely(int userId)
        {
            return GetUser(userId)
                .Tap(user => Console.WriteLine($"User retrieved: {user.Name}"))
                .TapError(error => Console.WriteLine($"Error occurred: {error.Message}, using default"))
                .OrElse(error => GetDefaultUser())
                .Tap(user => Console.WriteLine($"Final result: {user.Name}"));
        }

        // ============================================
        // EXAMPLE 11: Async pipeline with TapAsync
        // ============================================

        public static async Task<Result<User, Error>> ProcessUserAsync(int userId)
        {
            var userResult = GetUser(userId);

            userResult = await userResult.TapAsync(async user =>
            {
                await Task.Delay(10);
                Console.WriteLine($"[ASYNC] Processing user: {user.Name}");
            });

            return userResult;
        }

        // ============================================
        // EXAMPLE 12: EnsureAsync with async validation
        // ============================================

        public static async Task<Result<User, Error>> GetVerifiedUserAsync(int userId)
        {
            var userResult = GetUser(userId);

            return await userResult.EnsureAsync(
                async user =>
                {
                    // Simulate async verification check
                    await Task.Delay(20);
                    return user.Id % 2 == 0; // Even IDs are "verified"
                },
                user => new Error("NOT_VERIFIED", $"User {user.Name} is not verified"));
        }

        // ============================================
        // EXAMPLE 13: ZipLeft and ZipRight
        // ============================================

        public static void ZipExamples()
        {
            var user = GetUser(1);
            var age = GetUserAge(1);

            // ZipLeft - keeps first value if both succeed
            var leftResult = user.ZipLeft(age);
            Console.WriteLine($"ZipLeft: {leftResult.Match(u => u.Name, e => "Failed")}");

            // ZipRight - keeps second value if both succeed
            var rightResult = user.ZipRight(age);
            Console.WriteLine($"ZipRight: {rightResult.ValueOr(-1)}");
        }

        // ============================================
        // EXAMPLE 14: Practical use case - User activation
        // ============================================

        public static Result<string, Error> ActivateUser(int userId)
        {
            return GetUser(userId)
                .Tap(u => Console.WriteLine($"Attempting to activate user {u.Name}..."))
                .Ensure(u => u.Id > 0, new Error("INVALID_USER", "Invalid user ID"))
                .Ensure(u => !string.IsNullOrEmpty(u.Email), new Error("NO_EMAIL", "Email required for activation"))
                .Map(u => $"User {u.Name} activated successfully! Confirmation sent to {u.Email}")
                .TapError(e => Console.WriteLine($"Activation failed: {e.Message}"));
        }

        // ============================================
        // Helper methods
        // ============================================

        private static Result<User, Error> GetUser(int userId)
        {
            if (userId <= 0)
                return new Error("INVALID_ID", "User ID must be positive");

            if (userId > 100)
                return new Error("NOT_FOUND", $"User {userId} not found");

            return new User
            {
                Id = userId,
                Name = $"User {userId}",
                Email = userId % 3 == 0 ? "" : $"user{userId}@example.com"
            };
        }

        private static Result<int, Error> GetUserAge(int userId)
        {
            if (userId <= 0)
                return new Error("INVALID_ID", "User ID must be positive");

            return 25 + (userId % 40);
        }

        private static Result<User, Error> GetUserFromCache(int userId)
        {
            return new Error("CACHE_MISS", "User not in cache");
        }

        private static Result<User, Error> GetUserFromDatabase(int userId)
        {
            if (userId == 42)
                return new User { Id = userId, Name = "DB User", Email = "db@example.com" };

            return new Error("DB_MISS", "User not in database");
        }

        private static Result<User, Error> GetUserFromApi(int userId)
        {
            if (userId <= 50)
                return new User { Id = userId, Name = $"API User {userId}", Email = $"api{userId}@example.com" };

            return new Error("API_ERROR", "API request failed");
        }

        private static Result<User, Error> GetDefaultUser()
        {
            return new User { Id = 0, Name = "Guest", Email = "guest@example.com" };
        }

        // ============================================
        // MAIN - Demo all examples
        // ============================================

        public static async Task Main()
        {
            Console.WriteLine("=== Result Extensions Examples ===\n");

            // Example 1: Tap with logging
            Console.WriteLine("Example 1: Tap - Logging");
            GetUserWithLogging(1);
            GetUserWithLogging(999);
            Console.WriteLine();

            // Example 2: Ensure - Validation
            Console.WriteLine("Example 2: Ensure - Validation chains");
            var validatedResult = GetActiveUserWithEmail(2);
            validatedResult.Match(
                user => Console.WriteLine($"Valid user: {user.Name}"),
                error => Console.WriteLine($"Validation failed: {error.Message}")
            );
            Console.WriteLine();

            // Example 3: OrElse - Fallbacks
            Console.WriteLine("Example 3: OrElse - Fallback strategy");
            var fallbackResult = GetUserWithFallback(42);
            Console.WriteLine($"Found user: {fallbackResult.Match(u => u.Name, e => "Not found")}");
            Console.WriteLine();

            // Example 4: Recover
            Console.WriteLine("Example 4: Recover - Default value");
            var email1 = GetUserEmailWithDefault(1);
            var email2 = GetUserEmailWithDefault(999);
            Console.WriteLine($"Email 1: {email1.ValueOr("error")}");
            Console.WriteLine($"Email 2: {email2.ValueOr("error")}");
            Console.WriteLine();

            // Example 5: Zip
            Console.WriteLine("Example 5: Zip - Combining results");
            var summary = GetUserSummary(1);
            Console.WriteLine(summary.ValueOr("Could not generate summary"));
            Console.WriteLine();

            // Example 6: BiMap
            Console.WriteLine("Example 6: BiMap - Transform both paths");
            var display1 = GetUserDisplayInfo(1);
            var display2 = GetUserDisplayInfo(999);
            Console.WriteLine(display1.ValueOr("Error"));
            Console.WriteLine(display2.ValueOr("Error"));
            Console.WriteLine();

            // Example 7: ToNullable
            Console.WriteLine("Example 7: ToNullable");
            var age = GetUserAgeOrNull(1);
            var user = GetUserOrNull(999);
            Console.WriteLine($"Age: {age?.ToString() ?? "null"}");
            Console.WriteLine($"User: {user?.Name ?? "null"}");
            Console.WriteLine();

            // Example 8: MapAsync
            Console.WriteLine("Example 8: MapAsync - Async mapping");
            var avatarResult = await GetUserAvatarUrlAsync(1);
            Console.WriteLine($"Avatar URL: {avatarResult.ValueOr("No avatar")}");
            Console.WriteLine();

            // Example 9: Complex validation pipeline
            Console.WriteLine("Example 9: Validation pipeline");
            ValidateUserPipeline(1);
            Console.WriteLine();

            // Example 10: Safe retrieval
            Console.WriteLine("Example 10: Safe retrieval with fallback");
            GetUserSafely(999);
            Console.WriteLine();

            // Example 11: Async processing
            Console.WriteLine("Example 11: Async processing");
            await ProcessUserAsync(1);
            Console.WriteLine();

            // Example 12: Async validation
            Console.WriteLine("Example 12: Async validation");
            var verified = await GetVerifiedUserAsync(2);
            Console.WriteLine($"Verified: {verified.Match(u => u.Name, e => e.Message)}");
            Console.WriteLine();

            // Example 13: ZipLeft/ZipRight
            Console.WriteLine("Example 13: ZipLeft and ZipRight");
            ZipExamples();
            Console.WriteLine();

            // Example 14: Practical activation
            Console.WriteLine("Example 14: User activation");
            var activation = ActivateUser(1);
            Console.WriteLine(activation.ValueOr("Activation failed"));
        }
    }
}
