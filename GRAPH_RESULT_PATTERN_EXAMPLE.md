# Result Pattern for Graph API - Practical Example

Based on your QBE codebase, here's how Result pattern would improve Graph API operations.

## Current Problems in Your Code

### 1. **Silent Null Returns** (Email Service)
```csharp
// Current code - line 105-114
public async Task<string?> GetMimeEmail(string emailId)
{
    var request = await GraphApiRequest("users/" + mailBox + "/messages/" + emailId + "/$value");
    var response = await ExecuteRequestAsync(request);
    if (!response.IsSuccessStatusCode)
    {
        return null;  // ❌ Why did it fail? Timeout? 404? Auth error?
    }
    return response.Content;
}
```

**Problems:**
- Caller doesn't know WHY it failed
- Can't distinguish between 404 (not found) vs 503 (rate limit) vs timeout
- No way to handle retries intelligently
- Loses error context

### 2. **Boolean Success Flags** (Email Move)
```csharp
// Current code - line 55-103
public async Task<bool> MoveReadEmailFromInbox(IList<QbeEmail> messages)
{
    bool ret = false;
    foreach (var chunk in messages.Chunk(MAX_GRAPH_BATCH_SIZE))
    {
        // ... batch operations ...
        var res = await ExecuteRequestAsync<BatchResponseContentCollection>(batch);
        ret = res.IsSuccessStatusCode;  // ❌ Only last chunk's status!
    }
    return ret;  // ❌ Lost all error details
}
```

**Problems:**
- Only returns last chunk's status
- No info about which specific emails failed
- Can't retry just failed items

### 3. **Exception on Not Found** (SharePoint)
```csharp
// Current code - line 154-162
public static string UrlFromLibraryId(string? libraryId)
{
    if (string.IsNullOrEmpty(libraryId))
    {
        throw new QbeException("205", "Library not found");  // ❌ Exception for expected case
    }
    return $"/sites/{SITE_ID}/drives/{libraryId}";
}
```

**Problem:**
- Throws exception for an expected scenario (library might not exist)
- Expensive exception handling for common case

---

## Solution: Result Pattern

### Step 1: Define Graph-Specific Error Types

```csharp
namespace Qbe.Graph
{
    // Base Graph error
    public record GraphError(string Code, string Message, Exception? InnerException = null);

    // Specific error types
    public record NotFoundGraphError(string ResourceType, string ResourceId)
        : GraphError("NOT_FOUND", $"{ResourceType} '{ResourceId}' not found");

    public record RateLimitError(int RetryAfterSeconds)
        : GraphError("RATE_LIMIT", $"Rate limited. Retry after {RetryAfterSeconds}s");

    public record AuthenticationError(string Reason)
        : GraphError("AUTH_ERROR", $"Authentication failed: {Reason}");

    public record TimeoutError(int TimeoutMs)
        : GraphError("TIMEOUT", $"Request timed out after {TimeoutMs}ms");

    public record PermissionError(string Resource, string RequiredPermission)
        : GraphError("PERMISSION_DENIED", $"Missing permission '{RequiredPermission}' for {Resource}");

    public record NetworkError(string Message, Exception? InnerException = null)
        : GraphError("NETWORK_ERROR", Message, InnerException);

    public record ValidationError(string Field, string Message)
        : GraphError("VALIDATION_ERROR", $"{Field}: {Message}");
}
```

### Step 2: Refactor Email Service with Result Pattern

```csharp
using Qbe.Utils; // Your Result utilities

namespace Qbe.Graph
{
    public class QbeEmailGraphService : QbeGraphService
    {
        // ✅ Now caller knows exactly what went wrong
        public async Task<Result<string, GraphError>> GetMimeEmailAsync(string emailId)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(emailId))
                return new ValidationError("emailId", "Email ID cannot be empty");

            var request = await GraphApiRequest($"users/{mailBox}/messages/{emailId}/$value");

            return await ResultHelpers.TryAsync(
                async () =>
                {
                    var response = await ExecuteRequestAsync(request);

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.OK => Result<string, GraphError>.Success(response.Content),
                        System.Net.HttpStatusCode.NotFound => new NotFoundGraphError("Email", emailId),
                        System.Net.HttpStatusCode.Unauthorized => new AuthenticationError("Token expired or invalid"),
                        System.Net.HttpStatusCode.TooManyRequests => new RateLimitError(
                            int.Parse(response.Headers?.FirstOrDefault(h => h.Name == "Retry-After")?.Value?.ToString() ?? "60")),
                        System.Net.HttpStatusCode.RequestTimeout => new TimeoutError(30000),
                        _ => new NetworkError($"HTTP {(int)response.StatusCode}: {response.StatusDescription}")
                    };
                },
                ex => new NetworkError("Request failed", ex)
            );
        }

        // ✅ Detailed results for each email operation
        public async Task<Result<BatchMoveResult, GraphError>> MoveReadEmailFromInboxAsync(IList<QbeEmail> messages)
        {
            if (messages == null || messages.Count == 0)
                return new ValidationError("messages", "Message list cannot be empty");

            var allResults = new List<EmailMoveResult>();

            foreach (var chunk in messages.Chunk(MAX_GRAPH_BATCH_SIZE))
            {
                // Process each chunk and collect results
                var chunkResult = await ProcessEmailChunk(chunk);

                if (chunkResult.IsFailure)
                {
                    // Batch request failed - return error with partial results
                    return new NetworkError($"Batch failed after {allResults.Count} successful moves");
                }

                allResults.AddRange(chunkResult.Value);
            }

            var successful = allResults.Where(r => r.Success).ToList();
            var failed = allResults.Where(r => !r.Success).ToList();

            return new BatchMoveResult(successful, failed);
        }

        private async Task<Result<List<EmailMoveResult>, GraphError>> ProcessEmailChunk(IEnumerable<QbeEmail> chunk)
        {
            var steps = new List<object>();
            int cptId = 1;

            foreach (var message in chunk)
            {
                var destFolder = message.status switch
                {
                    QbeEmailStatus.Relevant => okFolderDest,
                    QbeEmailStatus.Error => errorFodler,
                    _ => noOkFolderDest
                };

                var emailRequest = await GraphApiRequest($"users/{mailBox}/messages/{message.messageId}/move", Method.Post, absoluteUrl: true, addToken: false);
                var body = new Dictionary<string, object> { { "DestinationId", destFolder } };
                emailRequest.AddBody(body);

                steps.Add(new
                {
                    Id = cptId++,
                    Method = "POST",
                    Url = emailRequest.Resource,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                    Body = emailRequest.Parameters.First(x => x.Type == ParameterType.RequestBody).Value
                });
            }

            var batch = await GetQbeBatchRequest();
            batch.AddJsonBody(JsonConvert.SerializeObject(new { requests = steps }));

            return await ResultHelpers.TryAsync(
                async () =>
                {
                    var response = await ExecuteRequestAsync<BatchResponseContentCollection>(batch);

                    if (!response.IsSuccessStatusCode)
                    {
                        return Result<List<EmailMoveResult>, GraphError>.Failure(
                            new NetworkError($"Batch request failed: {response.RawResponse.StatusDescription}"));
                    }

                    // Parse individual responses
                    var results = chunk.Select((msg, index) => new EmailMoveResult(
                        msg.messageId,
                        true, // Parse from batch response
                        null
                    )).ToList();

                    return Result<List<EmailMoveResult>, GraphError>.Success(results);
                },
                ex => new NetworkError("Batch processing failed", ex)
            );
        }

        // ✅ Returns detailed info about what emails were processed
        public async Task<Result<EmailProcessingResult, GraphError>> ReadSharedMailBoxAndAnalyseEmailsAsync()
        {
            var filter = QbeConnection.IsProd ? "" : $"&$filter=contains(subject, '[{QbeConnection.Server}]')";
            var request = await GraphApiRequest($"users/{mailBox}/mailFolders/inbox/messages?$top=1{filter}");
            request.AddHeader("Prefer", "outlook.native");

            var fetchResult = await ResultHelpers.TryAsync(
                async () =>
                {
                    var response = await ExecuteRequestAsync<MessageCollectionResponse>(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        return response.RawResponse.StatusCode switch
                        {
                            System.Net.HttpStatusCode.NotFound =>
                                Result<MessageCollectionResponse, GraphError>.Failure(
                                    new NotFoundGraphError("Mailbox", mailBox)),
                            System.Net.HttpStatusCode.Unauthorized =>
                                Result<MessageCollectionResponse, GraphError>.Failure(
                                    new AuthenticationError("Cannot access mailbox")),
                            System.Net.HttpStatusCode.Forbidden =>
                                Result<MessageCollectionResponse, GraphError>.Failure(
                                    new PermissionError(mailBox, "Mail.Read")),
                            _ => Result<MessageCollectionResponse, GraphError>.Failure(
                                new NetworkError($"Failed to fetch emails: {response.RawResponse.StatusDescription}"))
                        };
                    }

                    return Result<MessageCollectionResponse, GraphError>.Success(response.Data);
                },
                ex => new NetworkError("Email fetch failed", ex)
            );

            if (fetchResult.IsFailure)
                return fetchResult.Error;

            var messageCollection = fetchResult.Value;

            // Process each message with Result pattern
            var processedEmails = await ResultHelpers.TraverseAsync(
                messageCollection.Value,
                async message => await ProcessSingleEmail(message)
            );

            if (processedEmails.IsFailure)
                return processedEmails.Error;

            return new EmailProcessingResult(processedEmails.Value);
        }

        private async Task<Result<QbeEmail, GraphError>> ProcessSingleEmail(Message message)
        {
            // Move to busy folder
            var moveRequest = await GraphApiRequest($"users/{mailBox}/messages/{message.Id}/move", Method.Post);
            moveRequest.AddBody(new Dictionary<string, object> { { "DestinationId", busyFolder } });

            var moveResult = await ResultHelpers.TryAsync(
                async () => await ExecuteRequestAsync<Message>(moveRequest),
                ex => new NetworkError("Failed to move email to busy folder", ex)
            );

            if (moveResult.IsFailure)
                return moveResult.Error;

            var movedMessage = moveResult.Value.Data;
            var body = message.Body.Content;

            // Extract reference
            string patternRef = @"(?<=#REF:)[A-Z0-9/-]+(?=#)";
            var matchRef = Regex.Match(body, patternRef, RegexOptions.None, TimeSpan.FromSeconds(1));

            var email = new QbeEmail
            {
                messageId = movedMessage.Id,
                subject = message.Subject,
                from = message.Sender.EmailAddress.Address,
                tms = ((DateTimeOffset)message.ReceivedDateTime).ToLocalTime()
            };

            var server = QbeConnection.IsProd ? "" : $"[{QbeConnection.Server}]";
            var validSubjects = new[] { $"{server}Approve sign off", $"{server}Reject sign off" };

            if (matchRef.Success && !string.IsNullOrEmpty(matchRef.Value) &&
                validSubjects.Contains(message.Subject, StringComparer.InvariantCultureIgnoreCase))
            {
                email.reference = matchRef.Value;
                email.status = QbeEmailStatus.Relevant;
                email.body = QbeEmails.StripHTML(body).Replace($"#REF:{email.reference}#", "");
            }
            else
            {
                email.status = QbeEmailStatus.Ignored;
            }

            return Result<QbeEmail, GraphError>.Success(email);
        }
    }

    // Result DTOs
    public record EmailMoveResult(string MessageId, bool Success, string? ErrorMessage);
    public record BatchMoveResult(List<EmailMoveResult> Successful, List<EmailMoveResult> Failed);
    public record EmailProcessingResult(List<QbeEmail> Emails);
}
```

### Step 3: Refactor SharePoint Service

```csharp
namespace Qbe.Graph
{
    public class QbeSharepointService : QbeGraphService
    {
        // ✅ No exception for expected case
        public async Task<Result<string, GraphError>> GetLibraryIdForLibraryNameAsync(string libraryName)
        {
            if (string.IsNullOrWhiteSpace(libraryName))
                return new ValidationError("libraryName", "Library name cannot be empty");

            libraryName = libraryName == "" ? "Documents" : libraryName;
            var computedLibraryName = GetComputedLibraryName(libraryName);

            // Check cache
            if (_libraryIdCache.TryGetValue(computedLibraryName, out var driveId))
                return Result<string, GraphError>.Success(driveId);

            // Fallback to IT11 for non-prod
            if (QbeConnection.TestEnvName != "IT11" && computedLibraryName.EndsWith($"_{QbeConnection.TestEnvName}"))
            {
                if (_libraryIdCache.TryGetValue($"{libraryName}_IT11", out driveId))
                    return Result<string, GraphError>.Success(driveId);
            }

            // Fetch from Graph API
            var request = await GraphApiRequest($"/sites/{SITE_ID}/drives?$top=1000");

            return await ResultHelpers.TryAsync(
                async () =>
                {
                    var response = await ExecuteRequestAsync<DriveCollectionResponse>(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        return response.RawResponse.StatusCode switch
                        {
                            System.Net.HttpStatusCode.NotFound =>
                                Result<string, GraphError>.Failure(new NotFoundGraphError("Site", SITE_ID)),
                            System.Net.HttpStatusCode.Forbidden =>
                                Result<string, GraphError>.Failure(new PermissionError("Site", "Sites.Read.All")),
                            _ => Result<string, GraphError>.Failure(
                                new NetworkError($"Failed to get drives: {response.RawResponse.StatusDescription}"))
                        };
                    }

                    var drive = response.Data.Value?
                        .FirstOrDefault(d => d.Name.Equals(computedLibraryName, StringComparison.OrdinalIgnoreCase));

                    if (drive == null)
                        return Result<string, GraphError>.Failure(
                            new NotFoundGraphError("Library", computedLibraryName));

                    // Cache it
                    _libraryIdCache[computedLibraryName] = drive.Id;

                    return Result<string, GraphError>.Success(drive.Id);
                },
                ex => new NetworkError("Failed to fetch library", ex)
            );
        }

        // ✅ Can retry with intelligent backoff
        public async Task<Result<DriveItem, GraphError>> UploadFileWithRetryAsync(
            string libraryName,
            string folderPath,
            string fileName,
            byte[] content,
            int maxRetries = 3)
        {
            var attempt = 0;

            while (attempt < maxRetries)
            {
                var result = await UploadFileAsync(libraryName, folderPath, fileName, content);

                if (result.IsSuccess)
                    return result;

                // Handle retryable errors
                if (result.Error is RateLimitError rateLimitError)
                {
                    await Task.Delay(TimeSpan.FromSeconds(rateLimitError.RetryAfterSeconds));
                    attempt++;
                    continue;
                }

                if (result.Error is TimeoutError && attempt < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
                    attempt++;
                    continue;
                }

                // Non-retryable error
                return result.Error;
            }

            return new NetworkError($"Upload failed after {maxRetries} attempts");
        }

        private async Task<Result<DriveItem, GraphError>> UploadFileAsync(
            string libraryName,
            string folderPath,
            string fileName,
            byte[] content)
        {
            // Get library ID
            var libraryIdResult = await GetLibraryIdForLibraryNameAsync(libraryName);
            if (libraryIdResult.IsFailure)
                return libraryIdResult.Error;

            var libraryId = libraryIdResult.Value;

            // Validate file size
            if (content.Length > MAX_MB_FOR_BASIC_UPLOAD * BYTES_PER_MEGABYTE)
            {
                return await UploadLargeFileAsync(libraryId, folderPath, fileName, content);
            }

            // Upload file
            var uploadPath = Combine(folderPath, CleanFileName(fileName));
            var request = await GraphApiRequest(
                $"{UrlFromLibraryId(libraryId)}/root:/{uploadPath}:/content",
                Method.Put);
            request.AddBody(content, "application/octet-stream");

            return await ResultHelpers.TryAsync(
                async () =>
                {
                    var response = await ExecuteRequestAsync<DriveItem>(request);

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Created or System.Net.HttpStatusCode.OK =>
                            Result<DriveItem, GraphError>.Success(response.Data),
                        System.Net.HttpStatusCode.TooManyRequests =>
                            Result<DriveItem, GraphError>.Failure(new RateLimitError(60)),
                        System.Net.HttpStatusCode.RequestEntityTooLarge =>
                            Result<DriveItem, GraphError>.Failure(
                                new ValidationError("file", $"File too large: {content.Length / BYTES_PER_MEGABYTE}MB")),
                        _ => Result<DriveItem, GraphError>.Failure(
                            new NetworkError($"Upload failed: {response.RawResponse.StatusDescription}"))
                    };
                },
                ex => new NetworkError("Upload request failed", ex)
            );
        }
    }
}
```

### Step 4: Using in Controllers (Convert back to QbeException)

```csharp
// In your Web controller
public class QbeSharepointController : QbeControllerBase
{
    private readonly QbeSharepointService _sharepointService;

    [QbeRoute]
    [Route("UploadFile")]
    public async Task<IActionResult> UploadFile(string libraryName, string folder, IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var content = ms.ToArray();

        var result = await _sharepointService.UploadFileWithRetryAsync(
            libraryName,
            folder,
            file.FileName,
            content
        );

        // Convert Result to exception for existing error handling
        return result.Match<IActionResult>(
            driveItem => Ok(new { driveItem.Id, driveItem.Name, driveItem.WebUrl }),
            error => error switch
            {
                NotFoundGraphError notFound =>
                    throw new QbeException("205", notFound.Message),
                RateLimitError rateLimit =>
                    throw new QbeException("RATE_LIMIT", $"Try again in {rateLimit.RetryAfterSeconds}s", sendMail: false),
                ValidationError validation =>
                    throw new QbeException("VALIDATION", validation.Message, sendMail: false),
                PermissionError permission =>
                    throw new QbeException("37"), // Your existing permission error
                _ =>
                    throw new QbeException("205", error.Message)
            }
        );
    }

    // Or keep Result all the way to response
    [QbeRoute]
    [Route("GetLibrary")]
    public async Task<IActionResult> GetLibrary(string libraryName)
    {
        var result = await _sharepointService.GetLibraryIdForLibraryNameAsync(libraryName);

        return result.Match<IActionResult>(
            libraryId => Ok(new { libraryId }),
            error => error switch
            {
                NotFoundGraphError => NotFound(new { error.Code, error.Message }),
                PermissionError => Forbid(),
                _ => StatusCode(500, new { error.Code, error.Message })
            }
        );
    }
}
```

---

## Benefits You Get

### 1. **Intelligent Retry Logic**
```csharp
// Retry rate limits automatically
var result = await _sharepointService.UploadFileWithRetryAsync(...);

// Or use ResultHelpers for custom retry
var result = await ResultHelpers.TryAsync(
    async () => await _graphService.CallApi(),
    ex => new NetworkError("Failed", ex)
).OrElseAsync(async error =>
{
    if (error is RateLimitError)
    {
        await Task.Delay(1000);
        return await _graphService.CallApi(); // Retry
    }
    return error;
});
```

### 2. **Batch Operations with Partial Success**
```csharp
var moveResult = await _emailService.MoveReadEmailFromInboxAsync(emails);

moveResult.Match(
    batchResult =>
    {
        Console.WriteLine($"Moved: {batchResult.Successful.Count}");
        Console.WriteLine($"Failed: {batchResult.Failed.Count}");

        // Retry failed ones
        var failedEmails = emails.Where(e =>
            batchResult.Failed.Any(f => f.MessageId == e.messageId)).ToList();
        // ... retry logic
    },
    error => // Handle complete failure
);
```

### 3. **Composed Operations**
```csharp
// Chain multiple Graph calls safely
var result = await GetLibraryIdForLibraryNameAsync("Documents")
    .BindAsync(libraryId => GetFolderAsync(libraryId, "MyFolder"))
    .BindAsync(folder => UploadFileAsync(folder, "file.txt", content))
    .BindAsync(driveItem => ShareFileAsync(driveItem, permissions));

// If any step fails, you get detailed error about which step failed
```

### 4. **No Lost Context**
```csharp
// Current: null return loses context
var mime = await GetMimeEmail(emailId);
if (mime == null)
{
    // Why? Timeout? 404? Auth? Who knows!
}

// With Result: Know exactly what happened
var result = await GetMimeEmailAsync(emailId);
result.Match(
    mime => ProcessEmail(mime),
    error => error switch
    {
        NotFoundGraphError => Log("Email deleted"),
        RateLimitError => Log("Rate limited, will retry"),
        TimeoutError => Log("Timeout, check network"),
        AuthenticationError => Log("Token expired, refresh"),
        _ => Log("Unknown error")
    }
);
```

---

## Recommendation

**Implement Result pattern ONLY in the Graph project:**

1. ✅ `QbeEmailGraphService` - Email operations
2. ✅ `QbeSharepointService` - SharePoint file operations
3. ✅ `QbeGraphService` - Base Graph API calls
4. ❌ Rest of codebase - Keep current exception pattern

This gives you the benefits where they matter most (external APIs) without disrupting your existing architecture.
