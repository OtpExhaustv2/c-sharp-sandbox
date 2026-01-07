using Sandbox.utils;
using Sandbox.Utils;
using System.Net;
using System.Text.Json;

namespace Sandbox.examples
{
    public record ApiError(string Code, string Message, Exception? InnerException = null);

    public class ThirdPartyWrappingExamples
    {
        private static readonly HttpClient _httpClient = new();

        // ============================================
        // EXAMPLE 1: Basic HTTP call wrapping
        // ============================================

        public static async Task<Result<string, Exception>> GetUserJsonAsync(int userId)
        {
            return await ResultHelpers.TryAsync(async () =>
            {
                var response = await _httpClient.GetAsync($"https://api.example.com/users/{userId}");
                response.EnsureSuccessStatusCode(); // Throws if not 2xx
                return await response.Content.ReadAsStringAsync();
            });
        }

        // ============================================
        // EXAMPLE 2: HTTP with custom error type
        // ============================================

        public static async Task<Result<string, ApiError>> GetUserJsonWithApiErrorAsync(int userId)
        {
            return await ResultHelpers.TryAsync(
                async () =>
                {
                    var response = await _httpClient.GetAsync($"https://api.example.com/users/{userId}");
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                },
                ex => ex switch
                {
                    HttpRequestException httpEx => new ApiError("HTTP_ERROR", httpEx.Message, httpEx),
                    TaskCanceledException => new ApiError("TIMEOUT", "Request timed out", ex),
                    _ => new ApiError("UNKNOWN_ERROR", ex.Message, ex)
                });
        }

        // ============================================
        // EXAMPLE 3: HTTP with status code handling
        // ============================================

        public static async Task<Result<User, ApiError>> GetUserAsync(int userId)
        {
            return await ResultHelpers.TryValidateAsync<(HttpResponseMessage response, string json), User, ApiError>(
                async () =>
                {
                    var response = await _httpClient.GetAsync($"https://api.example.com/users/{userId}");
                    var json = await response.Content.ReadAsStringAsync();
                    return (response, json);
                },
                result =>
                {
                    var (response, json) = result;

                    // Validate HTTP status
                    if (!response.IsSuccessStatusCode)
                    {
                        return response.StatusCode switch
                        {
                            HttpStatusCode.NotFound => new ApiError("USER_NOT_FOUND", $"User {userId} not found"),
                            HttpStatusCode.Unauthorized => new ApiError("UNAUTHORIZED", "Authentication required"),
                            HttpStatusCode.Forbidden => new ApiError("FORBIDDEN", "Access denied"),
                            _ => new ApiError("HTTP_ERROR", $"HTTP {(int)response.StatusCode}")
                        };
                    }

                    // Parse JSON
                    try
                    {
                        var user = JsonSerializer.Deserialize<User>(json);
                        if (user == null)
                            return new ApiError("PARSE_ERROR", "Failed to deserialize user");

                        return user;
                    }
                    catch (JsonException ex)
                    {
                        return new ApiError("INVALID_JSON", "Invalid JSON response", ex);
                    }
                },
                ex => new ApiError("NETWORK_ERROR", "Network error occurred", ex)
            );
        }

        // ============================================
        // EXAMPLE 4: Chaining HTTP calls
        // ============================================

        public static async Task<Result<string, ApiError>> GetUserEmailAsync(int userId)
        {
            return await GetUserAsync(userId)
                .BindAsync(user =>
                {
                    if (string.IsNullOrEmpty(user.Email))
                        return Task.FromResult<Result<string, ApiError>>(
                            new ApiError("NO_EMAIL", "User has no email"));

                    return Task.FromResult<Result<string, ApiError>>(user.Email);
                });
        }

        // ============================================
        // EXAMPLE 5: File I/O wrapping
        // ============================================

        public static Result<string, ApiError> ReadConfigFile(string path)
        {
            return ResultHelpers.Try(
                () => File.ReadAllText(path),
                ex => ex switch
                {
                    FileNotFoundException => new ApiError("FILE_NOT_FOUND", $"Config file not found: {path}", ex),
                    UnauthorizedAccessException => new ApiError("ACCESS_DENIED", "Permission denied", ex),
                    IOException ioEx => new ApiError("IO_ERROR", ioEx.Message, ioEx),
                    _ => new ApiError("UNKNOWN_ERROR", ex.Message, ex)
                });
        }

        // ============================================
        // EXAMPLE 6: Database-like operations
        // ============================================

        public static async Task<Result<User, ApiError>> SaveUserAsync(User user)
        {
            return await ResultHelpers.TryAsync(
                async () =>
                {
                    // Simulate database call that might throw
                    await Task.Delay(100);

                    if (string.IsNullOrEmpty(user.Email))
                        throw new InvalidOperationException("Email is required");

                    // Simulate save
                    return user;
                },
                ex => ex switch
                {
                    InvalidOperationException => new ApiError("VALIDATION_ERROR", ex.Message, ex),
                    TimeoutException => new ApiError("TIMEOUT", "Database timeout", ex),
                    _ => new ApiError("DATABASE_ERROR", "Failed to save user", ex)
                });
        }

        // ============================================
        // EXAMPLE 7: Catching specific exceptions only
        // ============================================

        public static Result<int, HttpRequestException> ParseHttpResponseCode(string url)
        {
            // Only catches HttpRequestException, lets others propagate
            return ResultHelpers.TryCatch<int, HttpRequestException>(() =>
            {
                var response = _httpClient.Send(new HttpRequestMessage(HttpMethod.Head, url));
                return (int)response.StatusCode;
            });
        }

        // ============================================
        // EXAMPLE 9: Retry logic with Result
        // ============================================

        public static async Task<Result<T, ApiError>> RetryAsync<T>(
            Func<Task<Result<T, ApiError>>> operation,
            int maxRetries = 3)
        {
            ApiError? lastError = null;

            for (int i = 0; i < maxRetries; i++)
            {
                var result = await operation();

                if (result.IsSuccess)
                    return result;

                lastError = result.Error;

                // Don't retry on certain errors
                if (lastError.Code == "USER_NOT_FOUND" || lastError.Code == "UNAUTHORIZED")
                    return result;

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // Exponential backoff
            }

            return new ApiError("MAX_RETRIES", $"Failed after {maxRetries} attempts", lastError?.InnerException);
        }

        public static async Task<Result<User, ApiError>> GetUserWithRetryAsync(int userId)
        {
            return await RetryAsync(() => GetUserAsync(userId));
        }

        // ============================================
        // EXAMPLE 10: Parallel operations with Result
        // ============================================

        public static async Task<Result<User[], ApiError>> GetMultipleUsersAsync(params int[] userIds)
        {
            return await ResultHelpers.TryAsync(
                async () =>
                {
                    var tasks = userIds.Select(id => GetUserAsync(id)).ToArray();
                    var results = await Task.WhenAll(tasks);

                    // Check if any failed
                    var firstError = results.FirstOrDefault(r => r.IsFailure);
                    if (firstError.IsFailure)
                        throw new InvalidOperationException(firstError.Error.Message);

                    return results.Select(r => r.Value).ToArray();
                },
                ex => new ApiError("BATCH_ERROR", "Failed to fetch multiple users", ex)
            );
        }

        // ============================================
        // EXAMPLE 11: Using extension method style
        // ============================================

        public static async Task<Result<User, ApiError>> GetUserExtensionStyleAsync(int userId)
        {
            // Extension method on HttpClient (you'd need to create this)
            return await _httpClient
                .GetAsync($"https://api.example.com/users/{userId}")
                .ToResultAsync(
                    async response =>
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        return JsonSerializer.Deserialize<User>(json)!;
                    },
                    ex => new ApiError("HTTP_ERROR", ex.Message, ex)
                );
        }

        // ============================================
        // EXAMPLE 12: Circuit breaker pattern with Result
        // ============================================

        private static int _failureCount = 0;
        private static DateTime? _circuitOpenedAt = null;
        private const int FailureThreshold = 5;
        private static readonly TimeSpan CircuitResetTimeout = TimeSpan.FromMinutes(1);

        public static async Task<Result<T, ApiError>> WithCircuitBreakerAsync<T>(
            Func<Task<Result<T, ApiError>>> operation)
        {
            // Check if circuit is open
            if (_circuitOpenedAt.HasValue)
            {
                if (DateTime.UtcNow - _circuitOpenedAt.Value < CircuitResetTimeout)
                {
                    return new ApiError("CIRCUIT_OPEN", "Circuit breaker is open");
                }

                // Reset circuit
                _circuitOpenedAt = null;
                _failureCount = 0;
            }

            var result = await operation();

            if (result.IsFailure)
            {
                _failureCount++;
                if (_failureCount >= FailureThreshold)
                {
                    _circuitOpenedAt = DateTime.UtcNow;
                }
            }
            else
            {
                _failureCount = 0;
            }

            return result;
        }

        // ============================================
        // USAGE EXAMPLES
        // ============================================

        public static async Task Main()
        {
            Console.WriteLine("=== Third-Party Code Wrapping Examples ===\n");

            // Example 1: Basic HTTP call
            var result1 = await GetUserJsonAsync(1);
            result1.Match(
                json => Console.WriteLine($"Got JSON: {json[..50]}..."),
                ex => Console.WriteLine($"Error: {ex.Message}")
            );

            // Example 2: Custom error type
            var result2 = await GetUserJsonWithApiErrorAsync(1);
            result2.Match(
                json => Console.WriteLine($"Success: {json[..50]}..."),
                error => Console.WriteLine($"API Error [{error.Code}]: {error.Message}")
            );

            // Example 3: Full user object
            var result3 = await GetUserAsync(1);
            var userName = result3
                .Map(user => user.Name)
                .ValueOr("Unknown");
            Console.WriteLine($"User name: {userName}");

            // Example 4: Chained operations
            var emailResult = await GetUserEmailAsync(1);
            Console.WriteLine($"Email: {emailResult.ValueOr("No email")}");

            // Example 5: File I/O
            var configResult = ReadConfigFile("config.json");
            configResult.OnFailure(error =>
                Console.WriteLine($"Config error: {error.Message}"));

            // Example 9: With retry
            var retryResult = await GetUserWithRetryAsync(999);
            Console.WriteLine($"Retry result: {retryResult}");
        }
    }

    // ============================================
    // EXTENSION METHOD HELPERS (bonus)
    // ============================================

    public static class HttpClientResultExtensions
    {
        public static async Task<Result<T, TError>> ToResultAsync<T, TError>(
            this Task<HttpResponseMessage> responseTask,
            Func<HttpResponseMessage, Task<T>> successHandler,
            Func<Exception, TError> errorMapper)
        {
            try
            {
                var response = await responseTask;
                response.EnsureSuccessStatusCode();
                var result = await successHandler(response);
                return result;
            }
            catch (Exception ex)
            {
                return errorMapper(ex);
            }
        }
    }
}