using Sandbox.utils;

namespace Sandbox.Utils
{

    /// <summary>
    /// Helper methods to wrap exception-throwing code into Result pattern.
    /// </summary>
    public static class ResultHelpers
    {
        // ============================================
        // SYNCHRONOUS TRY WRAPPERS
        // ============================================

        /// <summary>
        /// Wraps a function that might throw into a Result.
        /// </summary>
        public static Result<T, Exception> Try<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Wraps a function that might throw into a Result with custom error type.
        /// </summary>
        public static Result<T, TError> Try<T, TError>(Func<T> func, Func<Exception, TError> errorMapper)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return errorMapper(ex);
            }
        }

        /// <summary>
        /// Wraps an action that might throw into a Result with Unit return.
        /// </summary>
        public static Result<Unit, Exception> Try(Action action)
        {
            try
            {
                action();
                return Unit.Value;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        // ============================================
        // ASYNCHRONOUS TRY WRAPPERS
        // ============================================

        /// <summary>
        /// Wraps an async function that might throw into a Result.
        /// </summary>
        public static async Task<Result<T, Exception>> TryAsync<T>(Func<Task<T>> func)
        {
            try
            {
                var result = await func();
                return result;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Wraps an async function that might throw into a Result with custom error type.
        /// </summary>
        public static async Task<Result<T, TError>> TryAsync<T, TError>(
            Func<Task<T>> func,
            Func<Exception, TError> errorMapper)
        {
            try
            {
                var result = await func();
                return result;
            }
            catch (Exception ex)
            {
                return errorMapper(ex);
            }
        }

        /// <summary>
        /// Wraps an async action that might throw into a Result with Unit return.
        /// </summary>
        public static async Task<Result<Unit, Exception>> TryAsync(Func<Task> func)
        {
            try
            {
                await func();
                return Unit.Value;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        // ============================================
        // SPECIFIC EXCEPTION HANDLING
        // ============================================

        /// <summary>
        /// Wraps a function and only catches specific exception types.
        /// Other exceptions are allowed to propagate.
        /// </summary>
        public static Result<T, TException> TryCatch<T, TException>(Func<T> func)
            where TException : Exception
        {
            try
            {
                return func();
            }
            catch (TException ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Wraps an async function and only catches specific exception types.
        /// </summary>
        public static async Task<Result<T, TException>> TryCatchAsync<T, TException>(Func<Task<T>> func)
            where TException : Exception
        {
            try
            {
                var result = await func();
                return result;
            }
            catch (TException ex)
            {
                return ex;
            }
        }

        // ============================================
        // ADVANCED WRAPPERS WITH VALIDATION
        // ============================================

        /// <summary>
        /// Wraps a function with both exception handling and result validation.
        /// </summary>
        public static Result<T, TError> TryValidate<T, TError>(
            Func<T> func,
            Func<T, Result<T, TError>> validator,
            Func<Exception, TError> errorMapper)
        {
            try
            {
                var result = func();
                return validator(result);
            }
            catch (Exception ex)
            {
                return errorMapper(ex);
            }
        }

        /// <summary>
        /// Wraps an async function with both exception handling and result validation.
        /// </summary>
        public static async Task<Result<T, TError>> TryValidateAsync<T, TError>(
            Func<Task<T>> func,
            Func<T, Result<T, TError>> validator,
            Func<Exception, TError> errorMapper)
        {
            try
            {
                var result = await func();
                return validator(result);
            }
            catch (Exception ex)
            {
                return errorMapper(ex);
            }
        }
    }

    /// <summary>
    /// Represents a void/Unit type for operations that don't return a value.
    /// </summary>
    public readonly struct Unit : IEquatable<Unit>
    {
        public static readonly Unit Value = new();

        public bool Equals(Unit other) => true;
        public override bool Equals(object? obj) => obj is Unit;
        public override int GetHashCode() => 0;
        public override string ToString() => "()";

        public static bool operator ==(Unit left, Unit right) => true;
        public static bool operator !=(Unit left, Unit right) => false;
    }

    public record ApiError(string Code, string Message, Exception? InnerException = null);
}
