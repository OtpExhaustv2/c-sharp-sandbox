using Sandbox.utils;
using Sandbox.Utils;

namespace Sandbox.examples
{
    public class CollectionExamples
    {
        // ============================================
        // EXAMPLE 1: Combine - All or nothing
        // ============================================

        public static Result<List<int>, Error> ValidateAllScores(int[] scores)
        {
            var results = scores.Select(score =>
                score >= 0 && score <= 100
                    ? Result<int, Error>.Success(score)
                    : Result<int, Error>.Failure(new Error("INVALID_SCORE", $"Score {score} is out of range"))
            );

            return ResultHelpers.Combine(results);
        }

        // ============================================
        // EXAMPLE 2: Partition - Separate successes and failures
        // ============================================

        public static void ProcessBatchWithPartition(int[] userIds)
        {
            var results = userIds.Select(id => GetUser(id));
            var (successes, failures) = ResultHelpers.Partition(results);

            Console.WriteLine($"Successfully processed: {successes.Count} users");
            Console.WriteLine($"Failed: {failures.Count} users");

            foreach (var user in successes)
            {
                Console.WriteLine($"  - {user.Name}");
            }

            foreach (var error in failures)
            {
                Console.WriteLine($"  - Error: {error.Message}");
            }
        }

        // ============================================
        // EXAMPLE 3: Traverse - Transform and validate
        // ============================================

        public static Result<List<User>, Error> ParseUserIds(string[] userIdStrings)
        {
            return ResultHelpers.Traverse(
                userIdStrings,
                str => int.TryParse(str, out var id)
                    ? GetUser(id)
                    : new Error("INVALID_ID", $"'{str}' is not a valid user ID")
            );
        }

        // ============================================
        // EXAMPLE 4: TraverseAsync - Async batch processing
        // ============================================

        public static async Task<Result<List<User>, ApiError>> FetchMultipleUsersAsync(int[] userIds)
        {
            return await ResultHelpers.TraverseAsync(
                userIds,
                async id => await FetchUserFromApiAsync(id)
            );
        }

        // ============================================
        // EXAMPLE 5: CollectAsync - Parallel operations
        // ============================================

        public static async Task<Result<List<User>, ApiError>> FetchUsersInParallelAsync(int[] userIds)
        {
            var tasks = userIds.Select(id => FetchUserFromApiAsync(id));
            return await ResultHelpers.CollectAsync(tasks);
        }

        // ============================================
        // EXAMPLE 6: CollectValues - Get all successes
        // ============================================

        public static List<User> GetAllValidUsers(int[] userIds)
        {
            var results = userIds.Select(id => GetUser(id));
            return ResultHelpers.CollectValues(results);
        }

        // ============================================
        // EXAMPLE 7: CollectErrors - Get all failures
        // ============================================

        public static List<Error> GetAllValidationErrors(string[] inputs)
        {
            var results = inputs.Select(input => ValidateInput(input));
            return ResultHelpers.CollectErrors(results);
        }

        // ============================================
        // EXAMPLE 8: Complex batch processing with error handling
        // ============================================

        public static async Task<Result<List<string>, ApiError>> ProcessUserBatchAsync(int[] userIds)
        {
            // Fetch all users (fails if any fails)
            var usersResult = await ResultHelpers.TraverseAsync(
                userIds,
                async id => await FetchUserFromApiAsync(id)
            );

            // Transform to email addresses
            return usersResult.Map(users =>
                users.Select(u => u.Email).ToList()
            );
        }

        // ============================================
        // EXAMPLE 9: Partial success handling
        // ============================================

        public static async Task ProcessWithPartialSuccess(int[] userIds)
        {
            var tasks = userIds.Select(id => FetchUserFromApiAsync(id));
            var results = await Task.WhenAll(tasks);

            var (successes, failures) = ResultHelpers.Partition(results);

            if (successes.Any())
            {
                Console.WriteLine($"Processed {successes.Count} users successfully:");
                foreach (var user in successes)
                {
                    Console.WriteLine($"  - {user.Name}: {user.Email}");
                }
            }

            if (failures.Any())
            {
                Console.WriteLine($"\n{failures.Count} failures:");
                foreach (var error in failures)
                {
                    Console.WriteLine($"  - [{error.Code}] {error.Message}");
                }
            }
        }

        // ============================================
        // EXAMPLE 10: Combining with validation
        // ============================================

        public static Result<List<User>, Error> GetValidUsersWithEmails(int[] userIds)
        {
            return ResultHelpers.Traverse(
                userIds,
                id => GetUser(id).Bind(user =>
                    string.IsNullOrEmpty(user.Email)
                        ? new Error("NO_EMAIL", $"User {user.Name} has no email")
                        : Result<User, Error>.Success(user)
                )
            );
        }

        // ============================================
        // Helper methods
        // ============================================

        private static Result<User, Error> GetUser(int userId)
        {
            if (userId <= 0)
                return new Error("INVALID_USER", "User ID must be positive");

            if (userId > 100)
                return new Error("USER_NOT_FOUND", $"User {userId} not found");

            return new User
            {
                Id = userId,
                Name = $"User {userId}",
                Email = userId % 2 == 0 ? $"user{userId}@example.com" : ""
            };
        }

        private static async Task<Result<User, ApiError>> FetchUserFromApiAsync(int userId)
        {
            await Task.Delay(10); // Simulate API call

            if (userId <= 0)
                return new ApiError("INVALID_USER", "User ID must be positive");

            if (userId > 100)
                return new ApiError("USER_NOT_FOUND", $"User {userId} not found");

            return new User
            {
                Id = userId,
                Name = $"User {userId}",
                Email = $"user{userId}@example.com"
            };
        }

        private static Result<string, Error> ValidateInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new Error("EMPTY_INPUT", "Input cannot be empty");

            if (input.Length < 3)
                return new Error("TOO_SHORT", "Input must be at least 3 characters");

            return input;
        }

        // ============================================
        // MAIN - Demo all examples
        // ============================================

        public static async Task Main()
        {
            Console.WriteLine("=== Collection Operations Examples ===\n");

            // Example 1: Combine
            Console.WriteLine("Example 1: Combine - Validate all scores");
            var scoresResult = ValidateAllScores(new[] { 85, 90, 102, 75 });
            scoresResult.Match(
                scores => Console.WriteLine($"All scores valid: {string.Join(", ", scores)}"),
                error => Console.WriteLine($"Validation failed: {error.Message}")
            );
            Console.WriteLine();

            // Example 2: Partition
            Console.WriteLine("Example 2: Partition - Process batch");
            ProcessBatchWithPartition(new[] { 1, 2, 150, 3, -5, 4 });
            Console.WriteLine();

            // Example 3: Traverse
            Console.WriteLine("Example 3: Traverse - Parse user IDs");
            var parseResult = ParseUserIds(new[] { "1", "2", "abc", "3" });
            parseResult.Match(
                users => Console.WriteLine($"Parsed {users.Count} users"),
                error => Console.WriteLine($"Parse error: {error.Message}")
            );
            Console.WriteLine();

            // Example 4: TraverseAsync
            Console.WriteLine("Example 4: TraverseAsync - Fetch multiple users");
            var fetchResult = await FetchMultipleUsersAsync(new[] { 1, 2, 3 });
            fetchResult.Match(
                users => Console.WriteLine($"Fetched {users.Count} users: {string.Join(", ", users.Select(u => u.Name))}"),
                error => Console.WriteLine($"Fetch error: {error.Message}")
            );
            Console.WriteLine();

            // Example 5: CollectAsync - Parallel
            Console.WriteLine("Example 5: CollectAsync - Parallel fetch");
            var parallelResult = await FetchUsersInParallelAsync(new[] { 1, 2, 3, 4, 5 });
            parallelResult.Match(
                users => Console.WriteLine($"Fetched {users.Count} users in parallel"),
                error => Console.WriteLine($"Fetch error: {error.Message}")
            );
            Console.WriteLine();

            // Example 6: CollectValues
            Console.WriteLine("Example 6: CollectValues - Get all valid users");
            var validUsers = GetAllValidUsers(new[] { 1, 150, 2, -5, 3 });
            Console.WriteLine($"Valid users: {validUsers.Count}");
            Console.WriteLine();

            // Example 7: CollectErrors
            Console.WriteLine("Example 7: CollectErrors - Get all validation errors");
            var errors = GetAllValidationErrors(new[] { "ok", "", "ab", "good" });
            Console.WriteLine($"Validation errors: {errors.Count}");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error.Code}: {error.Message}");
            }
            Console.WriteLine();

            // Example 9: Partial success
            Console.WriteLine("Example 9: Partial success handling");
            await ProcessWithPartialSuccess(new[] { 1, 2, 150, 3, 4 });
            Console.WriteLine();

            // Example 10: Combining with validation
            Console.WriteLine("Example 10: Get valid users with emails");
            var emailUsersResult = GetValidUsersWithEmails(new[] { 2, 4, 6 }); // Even IDs have emails
            emailUsersResult.Match(
                users => Console.WriteLine($"Users with emails: {string.Join(", ", users.Select(u => u.Email))}"),
                error => Console.WriteLine($"Error: {error.Message}")
            );
        }
    }
}
