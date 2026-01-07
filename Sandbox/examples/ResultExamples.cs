using Sandbox.utils;

namespace Sandbox.examples
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // Basic error - simple and lightweight
    public record Error(string Code, string Message);

    // Indexed error - for array/collection validation
    public record IndexedError(string Code, string Message, int Index, string? AttemptedValue = null);

    // Field error - for form/DTO validation
    public record FieldError(string Field, string Code, string Message, string? AttemptedValue = null);

    // Rich error - for complex scenarios with metadata
    public record RichError(string Code, string Message, Dictionary<string, object>? Metadata = null)
    {
        public RichError WithMetadata(string key, object value)
        {
            var meta = Metadata ?? new Dictionary<string, object>();
            meta[key] = value;
            return this with { Metadata = meta };
        }

        public T? GetMetadata<T>(string key)
        {
            if (Metadata != null && Metadata.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }
    }

    public class ResultExamples
    {
        // Example 1: Implicit conversion from value
        public static Result<int, Error> GetUserAge(int userId)
        {
            if (userId > 0)
                return 25; // Implicit conversion from int to Result<int, Error>

            return new Error("INVALID_USER", "User ID must be positive"); // Implicit conversion from Error
        }

        // Example 2: Using Match for pattern matching
        public static void MatchExample()
        {
            var result = GetUserAge(1);

            var message = result.Match(
                onSuccess: age => $"User is {age} years old",
                onFailure: error => $"Error: {error.Message}"
            );

            Console.WriteLine(message);
        }

        // Example 3: Chaining with Map
        public static Result<string, Error> GetUserAgeDescription(int userId)
        {
            return GetUserAge(userId)
                .Map(age => age switch
                {
                    < 18 => "Minor",
                    < 65 => "Adult",
                    _ => "Senior"
                });
        }

        // Example 4: Chaining with Bind
        public static Result<User, Error> GetUser(int userId)
        {
            if (userId <= 0)
                return new Error("INVALID_USER", "User ID must be positive");

            return new User { Id = userId, Name = "John Doe", Email = "john@example.com" };
        }

        public static Result<string, Error> GetUserEmail(int userId)
        {
            return GetUser(userId)
                .Bind(user => string.IsNullOrEmpty(user.Email)
                    ? new Error("NO_EMAIL", "User has no email")
                    : Result<string, Error>.Success(user.Email));
        }

        // Example 5: Using OnSuccess and OnFailure for side effects
        public static void SideEffectsExample(int userId)
        {
            GetUser(userId)
                .OnSuccess(user => Console.WriteLine($"Found user: {user.Name}"))
                .OnFailure(error => Console.WriteLine($"Error: {error.Message}"));
        }

        // Example 6: ValueOr for default values
        public static string GetEmailOrDefault(int userId)
        {
            return GetUserEmail(userId)
                .ValueOr("no-reply@example.com");
        }

        // Example 7: TryGetValue pattern
        public static void TryGetExample(int userId)
        {
            var result = GetUser(userId);

            if (result.TryGetValue(out var user))
            {
                Console.WriteLine($"Success: {user.Name}");
            }
            else if (result.TryGetError(out var error))
            {
                Console.WriteLine($"Failed: {error.Message}");
            }
        }

        // Example 9: Async operations
        public static async Task<Result<User, Error>> GetUserAsync(int userId)
        {
            await Task.Delay(100); // Simulate async work

            if (userId <= 0)
                return new Error("INVALID_USER", "User ID must be positive");

            return new User { Id = userId, Name = "Jane Doe", Email = "jane@example.com" };
        }

        public static async Task<Result<string, Error>> GetUserNameAsync(int userId)
        {
            return await GetUserAsync(userId)
                .MapAsync(user => user.Name);
        }

        // Example 10: Complex async chaining
        public static async Task<Result<bool, Error>> SendEmailToUserAsync(int userId, string message)
        {
            return await GetUserAsync(userId)
                .BindAsync(async user =>
                {
                    if (string.IsNullOrEmpty(user.Email))
                        return new Error("NO_EMAIL", "User has no email");

                    await Task.Delay(50); // Simulate sending email
                    Console.WriteLine($"Sent email to {user.Email}");
                    return Result<bool, Error>.Success(true);
                });
        }

        // Example 12: Explicit casting
        public static void ExplicitCastingExample()
        {
            Result<int, Error> result = 42;

            if (result.IsSuccess)
            {
                int value = (int)result; // Explicit cast to extract value
                Console.WriteLine($"Value: {value}");
            }
        }

        // Example 13: Railway-oriented programming
        public static Result<string, Error> ProcessUserPipeline(int userId)
        {
            return GetUser(userId)
                .Bind(user => ValidateUser(user))
                .Bind(user => EnrichUser(user))
                .Map(user => $"Processed user: {user.Name}");
        }

        private static Result<User, Error> ValidateUser(User user)
        {
            if (string.IsNullOrEmpty(user.Email))
                return new Error("INVALID_EMAIL", "Email is required");

            return user;
        }

        private static Result<User, Error> EnrichUser(User user)
        {
            // Simulate enrichment
            user.Email = user.Email.ToLower();
            return user;
        }

        // Example 14: MapError to transform errors
        public static Result<User, string> GetUserWithStringError(int userId)
        {
            return GetUser(userId)
                .MapError(error => $"{error.Code}: {error.Message}");
        }

        // Example 15: Using with LINQ-style query (custom)
        public static Result<string, Error> QueryStyleExample(int userId)
        {
            return GetUser(userId)
                .Bind(user => GetUserAge(user.Id)
                    .Map(age => new { user, age }))
                .Map(data => $"{data.user.Name} is {data.age} years old");
        }

        // Main demo
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Result<T, TError> Examples ===\n");

            // Example 1
            Console.WriteLine("Example 1: Basic usage");
            var ageResult = GetUserAge(1);
            Console.WriteLine($"Result: {ageResult}\n");

            // Example 2
            Console.WriteLine("Example 2: Match");
            MatchExample();
            Console.WriteLine();

            // Example 3
            Console.WriteLine("Example 3: Map");
            var description = GetUserAgeDescription(1);
            Console.WriteLine($"Description: {description}\n");

            // Example 5
            Console.WriteLine("Example 5: Side effects");
            SideEffectsExample(1);
            Console.WriteLine();

            // Example 6
            Console.WriteLine("Example 6: ValueOr");
            var email = GetEmailOrDefault(-1);
            Console.WriteLine($"Email: {email}\n");

            // Example 9
            Console.WriteLine("Example 9: Async");
            var userAsync = await GetUserAsync(1);
            Console.WriteLine($"Async user: {userAsync}\n");

            // Example 10
            Console.WriteLine("Example 10: Async chaining");
            await SendEmailToUserAsync(1, "Hello!");
            Console.WriteLine();

            // Example 13
            Console.WriteLine("Example 13: Pipeline");
            var processed = ProcessUserPipeline(1);
            Console.WriteLine($"Processed: {processed}\n");
        }
    }
}