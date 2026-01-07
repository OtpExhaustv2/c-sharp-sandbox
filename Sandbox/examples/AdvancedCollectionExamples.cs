using Sandbox.utils;
using Sandbox.Utils;

namespace Sandbox.examples
{
    public class AdvancedCollectionExamples
    {
        // ============================================
        // EXAMPLE 1: CombineAll - Collect ALL errors with index
        // ============================================

        public static Result<List<int>, List<IndexedError>> ValidateAllInputs(string[] inputs)
        {
            var results = inputs.Select((input, index) =>
            {
                if (string.IsNullOrWhiteSpace(input))
                    return Result<int, IndexedError>.Failure(
                        new IndexedError("EMPTY", "Input is empty", index, input));

                if (!int.TryParse(input, out var value))
                    return Result<int, IndexedError>.Failure(
                        new IndexedError("INVALID", $"'{input}' is not a valid number", index, input));

                if (value < 0)
                    return Result<int, IndexedError>.Failure(
                        new IndexedError("NEGATIVE", $"{value} is negative", index, value.ToString()));

                return Result<int, IndexedError>.Success(value);
            });

            return ResultHelpers.CombineAll(results);
        }

        // ============================================
        // EXAMPLE 2: FirstSuccess - Fallback sources
        // ============================================

        public static Result<User, List<Error>> GetUserFromAnywhere(int userId)
        {
            return ResultHelpers.FirstSuccess(
                GetUserFromCache(userId),
                GetUserFromDatabase(userId),
                GetUserFromApi(userId)
            );
        }

        // ============================================
        // EXAMPLE 3: Sequence - Unwrap collection of results
        // ============================================

        public static Result<List<string>, Error> GetAllEmails(int[] userIds)
        {
            var results = userIds.Select(id => GetUser(id).Map(u => u.Email));
            return ResultHelpers.Sequence(results);
        }

        // ============================================
        // EXAMPLE 4: TraverseParallelAsync - Parallel processing
        // ============================================

        public static async Task<Result<List<User>, ApiError>> FetchUsersParallelAsync(int[] userIds)
        {
            return await ResultHelpers.TraverseParallelAsync(
                userIds,
                async id =>
                {
                    await Task.Delay(100); // Simulate API call
                    return GetUserFromApi(id).MapError(e => new ApiError(e.Code, e.Message));
                },
                maxDegreeOfParallelism: 5
            );
        }

        // ============================================
        // EXAMPLE 5: TraverseBatchAsync - Batch processing
        // ============================================

        public static async Task<Result<List<User>, ApiError>> ProcessInBatches(int[] userIds)
        {
            return await ResultHelpers.TraverseBatchAsync(
                userIds,
                async batch =>
                {
                    // Simulate batch API call
                    await Task.Delay(50);
                    var users = batch.Select(id => new User
                    {
                        Id = id,
                        Name = $"User {id}",
                        Email = $"user{id}@example.com"
                    }).ToList();

                    return Result<List<User>, ApiError>.Success(users);
                },
                batchSize: 10
            );
        }

        // ============================================
        // EXAMPLE 6: PartitionMap - Transform both paths
        // ============================================

        public static (List<string> successMessages, List<string> errorMessages) ProcessAndFormat(int[] userIds)
        {
            var results = userIds.Select(id => GetUser(id));

            return ResultHelpers.PartitionMap(
                results,
                user => $"Success: {user.Name} ({user.Email})",
                error => $"Error: {error.Code} - {error.Message}"
            );
        }

        // ============================================
        // EXAMPLE 7: TryEach - Safe batch processing
        // ============================================

        public static List<Result<int, Exception>> ParseAllNumbers(string[] inputs)
        {
            return ResultHelpers.TryEach(
                inputs,
                input =>
                {
                    if (input == "boom")
                        throw new InvalidOperationException("Boom!");

                    return int.Parse(input); // May throw
                }
            );
        }

        // ============================================
        // EXAMPLE 8: TryEachAsync - Async safe processing
        // ============================================

        public static async Task<List<Result<string, Exception>>> FetchAllUrlsAsync(string[] urls)
        {
            return await ResultHelpers.TryEachAsync(
                urls,
                async url =>
                {
                    using var client = new HttpClient();
                    return await client.GetStringAsync(url);
                }
            );
        }

        // ============================================
        // EXAMPLE 9: Reduce - Accumulate with validation
        // ============================================

        public static Result<int, Error> SumWithLimit(int[] numbers, int maxSum)
        {
            return ResultHelpers.Reduce(
                numbers,
                0,
                (sum, number) =>
                {
                    var newSum = sum + number;
                    if (newSum > maxSum)
                        return new Error("LIMIT_EXCEEDED", $"Sum {newSum} exceeds limit {maxSum}");

                    return Result<int, Error>.Success(newSum);
                }
            );
        }

        // ============================================
        // EXAMPLE 10: ReduceAsync - Async accumulation
        // ============================================

        public static async Task<Result<List<string>, Error>> BuildReportAsync(int[] userIds)
        {
            return await ResultHelpers.ReduceAsync(
                userIds,
                new List<string>(),
                async (report, userId) =>
                {
                    await Task.Delay(10); // Simulate async work
                    var user = GetUser(userId);

                    if (user.IsFailure)
                        return user.Error;

                    report.Add($"{user.Value.Name}: {user.Value.Email}");
                    return Result<List<string>, Error>.Success(report);
                }
            );
        }

        // ============================================
        // EXAMPLE 11: GroupByResult - Grouping with validation
        // ============================================

        public static Result<Dictionary<string, List<User>>, Error> GroupUsersByDomain(int[] userIds)
        {
            var users = userIds.Select(GetUser).Where(r => r.IsSuccess).Select(r => r.Value);

            return ResultHelpers.GroupByResult(
                users,
                user =>
                {
                    if (string.IsNullOrEmpty(user.Email))
                        return new Error("NO_EMAIL", $"User {user.Name} has no email");

                    var domain = user.Email.Split('@').LastOrDefault();
                    if (string.IsNullOrEmpty(domain))
                        return new Error("INVALID_EMAIL", $"Invalid email: {user.Email}");

                    return Result<string, Error>.Success(domain);
                }
            );
        }

        // ============================================
        // EXAMPLE 12: SelectMany - Flatten nested results
        // ============================================

        public static Result<List<string>, Error> GetAllUserTags(int[] userIds)
        {
            var userResults = userIds.Select(GetUser);

            return ResultHelpers.SelectMany(
                userResults,
                user => new[] { $"user:{user.Id}", $"email:{user.Email}", $"name:{user.Name}" }
            );
        }

        // ============================================
        // EXAMPLE 13: CombineAll with FieldError for forms
        // ============================================

        public static void ValidateFormWithAllErrors(Dictionary<string, string> formData)
        {
            var username = formData.GetValueOrDefault("username") ?? "";
            var email = formData.GetValueOrDefault("email") ?? "";
            var password = formData.GetValueOrDefault("password") ?? "";
            var age = formData.GetValueOrDefault("age") ?? "";

            var validations = new List<Result<string, FieldError>>
            {
                ValidateUsername(username).MapError(e =>
                    new FieldError("username", e.Code, e.Message, username)),
                ValidateEmail(email).MapError(e =>
                    new FieldError("email", e.Code, e.Message, email)),
                ValidatePassword(password).MapError(e =>
                    new FieldError("password", e.Code, e.Message, "***")),
                ValidateAge(age).MapError(e =>
                    new FieldError("age", e.Code, e.Message, age)).Map(a => a.ToString())
            };

            var result = ResultHelpers.CombineAll(validations);

            result.Match(
                _ => Console.WriteLine("All validations passed!"),
                errors =>
                {
                    Console.WriteLine($"Validation failed with {errors.Count} errors:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"  - {error.Field}: [{error.Code}] {error.Message} (attempted: '{error.AttemptedValue}')");
                    }
                }
            );
        }

        // ============================================
        // EXAMPLE 13b: Using RichError with metadata
        // ============================================

        public static Result<string, RichError> ValidateWithRichError(string input, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return new RichError("EMPTY", "Value is required")
                    .WithMetadata("field", fieldName)
                    .WithMetadata("attemptedValue", input)
                    .WithMetadata("timestamp", DateTime.UtcNow);
            }

            if (input.Length < 3)
            {
                return new RichError("TOO_SHORT", "Value must be at least 3 characters")
                    .WithMetadata("field", fieldName)
                    .WithMetadata("attemptedValue", input)
                    .WithMetadata("minLength", 3)
                    .WithMetadata("actualLength", input.Length);
            }

            return input;
        }

        // ============================================
        // EXAMPLE 14: FirstSuccess with retry logic
        // ============================================

        public static async Task<Result<string, List<ApiError>>> FetchWithRetryAsync(string url)
        {
            var attempts = Enumerable.Range(1, 3).Select(async attempt =>
            {
                await Task.Delay(attempt * 100); // Exponential backoff
                return await TryFetchUrl(url, attempt);
            });

            var results = await Task.WhenAll(attempts);
            return ResultHelpers.FirstSuccess(results);
        }

        // ============================================
        // EXAMPLE 15: Complex pipeline with multiple operations
        // ============================================

        public static async Task<Result<Dictionary<string, List<string>>, List<Error>>> ProcessUserPipelineAsync(int[] userIds)
        {
            // Step 1: Fetch all users in parallel
            var fetchResult = await ResultHelpers.TraverseParallelAsync(
                userIds,
                async id =>
                {
                    await Task.Delay(10);
                    return GetUser(id);
                },
                maxDegreeOfParallelism: 10
            );

            if (fetchResult.IsFailure)
                return new List<Error> { fetchResult.Error };

            var users = fetchResult.Value;

            // Step 2: Validate all users and collect all errors
            var validations = users.Select(user =>
                string.IsNullOrEmpty(user.Email)
                    ? Result<User, Error>.Failure(new Error("NO_EMAIL", $"User {user.Name} has no email"))
                    : Result<User, Error>.Success(user)
            );

            var validationResult = ResultHelpers.CombineAll(validations);

            if (validationResult.IsFailure)
                return validationResult.Error;

            // Step 3: Group by domain
            var groupedResult = ResultHelpers.GroupByResult(
                validationResult.Value,
                user =>
                {
                    var domain = user.Email.Split('@').LastOrDefault() ?? "";
                    return Result<string, Error>.Success(domain);
                }
            );

            if (groupedResult.IsFailure)
                return new List<Error> { groupedResult.Error };

            // Step 4: Transform to name lists
            var result = groupedResult.Value.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(u => u.Name).ToList()
            );

            return Result<Dictionary<string, List<string>>, List<Error>>.Success(result);
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

        private static Result<User, Error> GetUserFromCache(int userId)
        {
            return new Error("CACHE_MISS", "User not in cache");
        }

        private static Result<User, Error> GetUserFromDatabase(int userId)
        {
            if (userId == 42)
                return new User { Id = userId, Name = "DB User", Email = "db@example.com" };

            return new Error("DB_ERROR", "User not in database");
        }

        private static Result<User, Error> GetUserFromApi(int userId)
        {
            if (userId <= 100)
                return new User { Id = userId, Name = $"API User {userId}", Email = $"api{userId}@example.com" };

            return new Error("API_ERROR", "API failed");
        }

        private static Result<string, Error> ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return new Error("USERNAME_REQUIRED", "Username is required");

            if (username.Length < 3)
                return new Error("USERNAME_TOO_SHORT", "Username must be at least 3 characters");

            return username;
        }

        private static Result<string, Error> ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return new Error("EMAIL_REQUIRED", "Email is required");

            if (!email.Contains('@'))
                return new Error("INVALID_EMAIL", "Email must contain @");

            return email;
        }

        private static Result<string, Error> ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return new Error("PASSWORD_REQUIRED", "Password is required");

            if (password.Length < 8)
                return new Error("PASSWORD_TOO_SHORT", "Password must be at least 8 characters");

            return password;
        }

        private static Result<int, Error> ValidateAge(string age)
        {
            if (!int.TryParse(age, out var ageValue))
                return new Error("INVALID_AGE", "Age must be a number");

            if (ageValue < 18)
                return new Error("AGE_TOO_YOUNG", "Must be at least 18 years old");

            return ageValue;
        }

        private static async Task<Result<string, ApiError>> TryFetchUrl(string url, int attempt)
        {
            // Simulate random failures
            if (new Random().Next(0, 2) == 0)
                return new ApiError("FETCH_ERROR", $"Attempt {attempt} failed");

            return $"Content from {url} (attempt {attempt})";
        }

        // ============================================
        // MAIN - Demo all examples
        // ============================================

        public static async Task Main()
        {
            Console.WriteLine("=== Advanced Collection Operations Examples ===\n");

            // Example 1: CombineAll with IndexedError
            Console.WriteLine("Example 1: CombineAll - Collect ALL errors with positions");
            var inputs = new[] { "10", "", "abc", "30", "-5" };
            var validateResult = ValidateAllInputs(inputs);
            validateResult.Match(
                values => Console.WriteLine($"All valid: {string.Join(", ", values)}"),
                errors =>
                {
                    Console.WriteLine($"Found {errors.Count} errors:");
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"  - Index {error.Index}: [{error.Code}] {error.Message} (value: '{error.AttemptedValue}')");
                    }
                }
            );
            Console.WriteLine();

            // Example 2: FirstSuccess
            Console.WriteLine("Example 2: FirstSuccess - Fallback sources");
            var userResult = GetUserFromAnywhere(42);
            userResult.Match(
                user => Console.WriteLine($"Found user: {user.Name}"),
                errors => Console.WriteLine($"All sources failed: {errors.Count} errors")
            );
            Console.WriteLine();

            // Example 3: Sequence
            Console.WriteLine("Example 3: Sequence - Get all emails");
            var emailsResult = GetAllEmails(new[] { 1, 2, 4, 5 });
            emailsResult.Match(
                emails => Console.WriteLine($"Emails: {string.Join(", ", emails)}"),
                error => Console.WriteLine($"Error: {error.Message}")
            );
            Console.WriteLine();

            // Example 4: TraverseParallelAsync
            Console.WriteLine("Example 4: TraverseParallelAsync - Parallel fetch");
            var parallelResult = await FetchUsersParallelAsync(new[] { 1, 2, 3, 4, 5 });
            parallelResult.Match(
                users => Console.WriteLine($"Fetched {users.Count} users in parallel"),
                error => Console.WriteLine($"Error: {error.Message}")
            );
            Console.WriteLine();

            // Example 6: PartitionMap
            Console.WriteLine("Example 6: PartitionMap - Transform both paths");
            var (successes, failures) = ProcessAndFormat(new[] { 1, 2, 150, 3 });
            Console.WriteLine("Successes:");
            successes.ForEach(s => Console.WriteLine($"  {s}"));
            Console.WriteLine("Failures:");
            failures.ForEach(f => Console.WriteLine($"  {f}"));
            Console.WriteLine();

            // Example 7: TryEach
            Console.WriteLine("Example 7: TryEach - Safe parsing");
            var parseResults = ParseAllNumbers(new[] { "10", "20", "boom", "30" });
            var (parseSuccesses, parseFailures) = ResultHelpers.Partition(parseResults);
            Console.WriteLine($"Parsed: {parseSuccesses.Count}, Failed: {parseFailures.Count}");
            Console.WriteLine();

            // Example 9: Reduce
            Console.WriteLine("Example 9: Reduce - Sum with limit");
            var sumResult = SumWithLimit(new[] { 10, 20, 30, 40 }, 80);
            sumResult.Match(
                sum => Console.WriteLine($"Sum: {sum}"),
                error => Console.WriteLine($"Error: {error.Message}")
            );
            Console.WriteLine();

            // Example 11: GroupByResult
            Console.WriteLine("Example 11: GroupByResult - Group by email domain");
            var groupResult = GroupUsersByDomain(new[] { 1, 2, 4, 5, 7, 8 });
            groupResult.Match(
                groups =>
                {
                    Console.WriteLine($"Grouped into {groups.Count} domains:");
                    foreach (var group in groups)
                    {
                        Console.WriteLine($"  {group.Key}: {group.Value.Count} users");
                    }
                },
                error => Console.WriteLine($"Error: {error.Message}")
            );
            Console.WriteLine();

            // Example 13: Form validation with FieldError
            Console.WriteLine("Example 13: Form validation with FieldError");
            var formData = new Dictionary<string, string>
            {
                ["username"] = "ab",
                ["email"] = "invalid",
                ["password"] = "short",
                ["age"] = "15"
            };
            ValidateFormWithAllErrors(formData);
            Console.WriteLine();

            // Example 13b: RichError with metadata
            Console.WriteLine("Example 13b: RichError with metadata");
            var richResult = ValidateWithRichError("ab", "username");
            richResult.Match(
                _ => Console.WriteLine("Validation passed"),
                error =>
                {
                    Console.WriteLine($"Error: [{error.Code}] {error.Message}");
                    Console.WriteLine("Metadata:");
                    if (error.Metadata != null)
                    {
                        foreach (var kvp in error.Metadata)
                        {
                            Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
                        }
                    }
                }
            );
            Console.WriteLine();

            // Example 15: Complex pipeline
            Console.WriteLine("Example 15: Complex pipeline");
            var pipelineResult = await ProcessUserPipelineAsync(new[] { 1, 2, 4, 5, 7, 8, 10 });
            pipelineResult.Match(
                groups =>
                {
                    Console.WriteLine($"Pipeline success! {groups.Count} domains:");
                    foreach (var group in groups)
                    {
                        Console.WriteLine($"  {group.Key}: {string.Join(", ", group.Value)}");
                    }
                },
                errors => Console.WriteLine($"Pipeline failed with {errors.Count} errors")
            );
        }
    }
}
