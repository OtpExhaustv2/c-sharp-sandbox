namespace sandbox_api.Models
{
    // Base database error
    public record DatabaseError(string Code, string Message, Exception? InnerException = null);

    // Specific error types for different scenarios
    public record NotFoundError(string EntityType, string EntityId)
        : DatabaseError("NOT_FOUND", $"{EntityType} with ID '{EntityId}' not found");

    public record ValidationError(string Field, string Message)
        : DatabaseError("VALIDATION_ERROR", $"Validation failed for field '{Field}': {Message}");

    public record DuplicateError(string Field, string Value)
        : DatabaseError("DUPLICATE", $"A record with {Field} '{Value}' already exists");

    public record ConcurrencyError(string EntityType, string EntityId)
        : DatabaseError("CONCURRENCY_ERROR", $"{EntityType} with ID '{EntityId}' has been modified by another user");

    public record InsufficientStockError(string ProductName, int Requested, int Available)
        : DatabaseError("INSUFFICIENT_STOCK", $"Insufficient stock for '{ProductName}'. Requested: {Requested}, Available: {Available}");

    public record InvalidOperationError(string Operation, string Reason)
        : DatabaseError("INVALID_OPERATION", $"Cannot perform '{Operation}': {Reason}");

    public record ConnectionError(string Message, Exception? InnerException = null)
        : DatabaseError("CONNECTION_ERROR", Message, InnerException);

    public record QueryError(string Query, Exception? InnerException = null)
        : DatabaseError("QUERY_ERROR", $"Query failed: {Query}", InnerException);
}
