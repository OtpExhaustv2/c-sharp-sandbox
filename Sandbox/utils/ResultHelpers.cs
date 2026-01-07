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

        /// <summary>
        /// Wraps an async function with both exception handling and result validation,
        /// allowing transformation from TInput to TOutput.
        /// </summary>
        public static async Task<Result<TOutput, TError>> TryValidateAsync<TInput, TOutput, TError>(
            Func<Task<TInput>> func,
            Func<TInput, Result<TOutput, TError>> validator,
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

        // ============================================
        // COLLECTION OPERATIONS
        // ============================================

        /// <summary>
        /// Combines multiple results into a single result containing an array.
        /// Returns the first error encountered, or success with all values.
        /// </summary>
        public static Result<T[], TError> Combine<T, TError>(params Result<T, TError>[] results)
        {
            var values = new T[results.Length];

            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].IsFailure)
                    return results[i].Error;

                values[i] = results[i].Value;
            }

            return values;
        }

        /// <summary>
        /// Combines multiple results into a single result containing a list.
        /// Returns the first error encountered, or success with all values.
        /// </summary>
        public static Result<List<T>, TError> Combine<T, TError>(IEnumerable<Result<T, TError>> results)
        {
            var values = new List<T>();

            foreach (var result in results)
            {
                if (result.IsFailure)
                    return result.Error;

                values.Add(result.Value);
            }

            return values;
        }

        /// <summary>
        /// Partitions a collection of results into two lists: successes and failures.
        /// </summary>
        public static (List<T> successes, List<TError> failures) Partition<T, TError>(
            IEnumerable<Result<T, TError>> results)
        {
            var successes = new List<T>();
            var failures = new List<TError>();

            foreach (var result in results)
            {
                if (result.IsSuccess)
                    successes.Add(result.Value);
                else
                    failures.Add(result.Error);
            }

            return (successes, failures);
        }

        /// <summary>
        /// Transforms a collection by applying a function that returns Result to each element.
        /// Returns success only if all transformations succeed, otherwise returns the first error.
        /// </summary>
        public static Result<List<TNew>, TError> Traverse<T, TNew, TError>(
            IEnumerable<T> items,
            Func<T, Result<TNew, TError>> mapper)
        {
            var results = new List<TNew>();

            foreach (var item in items)
            {
                var result = mapper(item);
                if (result.IsFailure)
                    return result.Error;

                results.Add(result.Value);
            }

            return results;
        }

        /// <summary>
        /// Async version of Traverse.
        /// Transforms a collection by applying an async function that returns Result to each element.
        /// </summary>
        public static async Task<Result<List<TNew>, TError>> TraverseAsync<T, TNew, TError>(
            IEnumerable<T> items,
            Func<T, Task<Result<TNew, TError>>> mapper)
        {
            var results = new List<TNew>();

            foreach (var item in items)
            {
                var result = await mapper(item);
                if (result.IsFailure)
                    return result.Error;

                results.Add(result.Value);
            }

            return results;
        }

        /// <summary>
        /// Collects results from already-started async tasks.
        /// Returns success only if all tasks complete successfully, otherwise returns the first error.
        /// </summary>
        public static async Task<Result<List<T>, TError>> CollectAsync<T, TError>(
            IEnumerable<Task<Result<T, TError>>> tasks)
        {
            var results = await Task.WhenAll(tasks);
            var arrayResult = Combine(results);

            // Convert from Result<T[], TError> to Result<List<T>, TError>
            return arrayResult.IsSuccess
                ? arrayResult.Value.ToList()
                : arrayResult.Error;
        }

        /// <summary>
        /// Collects all errors from a sequence of results.
        /// Returns empty list if all succeeded, or list of all errors.
        /// </summary>
        public static List<TError> CollectErrors<T, TError>(IEnumerable<Result<T, TError>> results)
        {
            return results
                .Where(r => r.IsFailure)
                .Select(r => r.Error)
                .ToList();
        }

        /// <summary>
        /// Collects all successful values from a sequence of results.
        /// Returns empty list if all failed, or list of all successful values.
        /// </summary>
        public static List<T> CollectValues<T, TError>(IEnumerable<Result<T, TError>> results)
        {
            return results
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToList();
        }

        // ============================================
        // ADVANCED COLLECTION OPERATIONS
        // ============================================

        /// <summary>
        /// Combines multiple results, collecting ALL errors instead of failing fast.
        /// Returns success only if all results succeed, otherwise returns all errors.
        /// </summary>
        public static Result<List<T>, List<TError>> CombineAll<T, TError>(IEnumerable<Result<T, TError>> results)
        {
            var values = new List<T>();
            var errors = new List<TError>();

            foreach (var result in results)
            {
                if (result.IsSuccess)
                    values.Add(result.Value);
                else
                    errors.Add(result.Error);
            }

            return errors.Count > 0
                ? Result<List<T>, List<TError>>.Failure(errors)
                : Result<List<T>, List<TError>>.Success(values);
        }

        /// <summary>
        /// Returns the first successful result, or a list of all errors if all fail.
        /// Useful for fallback/retry scenarios.
        /// </summary>
        public static Result<T, List<TError>> FirstSuccess<T, TError>(params Result<T, TError>[] results)
        {
            var errors = new List<TError>();

            foreach (var result in results)
            {
                if (result.IsSuccess)
                    return result.Value;

                errors.Add(result.Error);
            }

            return errors;
        }

        /// <summary>
        /// Returns the first successful result from an enumerable, or a list of all errors if all fail.
        /// </summary>
        public static Result<T, List<TError>> FirstSuccess<T, TError>(IEnumerable<Result<T, TError>> results)
        {
            var errors = new List<TError>();

            foreach (var result in results)
            {
                if (result.IsSuccess)
                    return result.Value;

                errors.Add(result.Error);
            }

            return errors;
        }

        /// <summary>
        /// Converts a collection of Results into a Result of a collection.
        /// Returns success only if all results succeed, otherwise returns the first error.
        /// This is essentially Combine but with a clearer name for the common "sequence" operation.
        /// </summary>
        public static Result<List<T>, TError> Sequence<T, TError>(IEnumerable<Result<T, TError>> results)
        {
            return Combine(results);
        }

        /// <summary>
        /// Async parallel version of Traverse with configurable parallelism.
        /// Processes items in parallel batches.
        /// </summary>
        public static async Task<Result<List<TNew>, TError>> TraverseParallelAsync<T, TNew, TError>(
            IEnumerable<T> items,
            Func<T, Task<Result<TNew, TError>>> mapper,
            int maxDegreeOfParallelism = 10)
        {
            var results = new List<TNew>();
            var itemsList = items.ToList();
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = itemsList.Select(async item =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await mapper(item);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var taskResults = await Task.WhenAll(tasks);

            foreach (var result in taskResults)
            {
                if (result.IsFailure)
                    return result.Error;

                results.Add(result.Value);
            }

            return results;
        }

        /// <summary>
        /// Processes items in batches, applying a batch processor function.
        /// Useful for API calls that support batch operations.
        /// </summary>
        public static async Task<Result<List<TNew>, TError>> TraverseBatchAsync<T, TNew, TError>(
            IEnumerable<T> items,
            Func<IEnumerable<T>, Task<Result<List<TNew>, TError>>> batchProcessor,
            int batchSize = 100)
        {
            var results = new List<TNew>();
            var itemsList = items.ToList();

            var chunks = items.Chunk(batchSize);

            foreach (var chunk in chunks)
            {
                var batchResult = await batchProcessor(chunk);

                if (batchResult.IsFailure)
                    return batchResult.Error;

                results.AddRange(batchResult.Value);
            }

            return results;
        }

        /// <summary>
        /// Partitions results and applies different transformations to successes and failures.
        /// </summary>
        public static (List<TSuccess> successes, List<TFailure> failures) PartitionMap<T, TError, TSuccess, TFailure>(
            IEnumerable<Result<T, TError>> results,
            Func<T, TSuccess> successMapper,
            Func<TError, TFailure> failureMapper)
        {
            var successes = new List<TSuccess>();
            var failures = new List<TFailure>();

            foreach (var result in results)
            {
                if (result.IsSuccess)
                    successes.Add(successMapper(result.Value));
                else
                    failures.Add(failureMapper(result.Error));
            }

            return (successes, failures);
        }

        /// <summary>
        /// Tries to apply a function to each item, catching exceptions for each individually.
        /// Returns a list of Results, one for each item.
        /// </summary>
        public static List<Result<TNew, Exception>> TryEach<T, TNew>(
            IEnumerable<T> items,
            Func<T, TNew> func)
        {
            var results = new List<Result<TNew, Exception>>();

            foreach (var item in items)
            {
                try
                {
                    results.Add(func(item));
                }
                catch (Exception ex)
                {
                    results.Add(ex);
                }
            }

            return results;
        }

        /// <summary>
        /// Async version of TryEach.
        /// </summary>
        public static async Task<List<Result<TNew, Exception>>> TryEachAsync<T, TNew>(
            IEnumerable<T> items,
            Func<T, Task<TNew>> func)
        {
            var results = new List<Result<TNew, Exception>>();

            foreach (var item in items)
            {
                try
                {
                    var value = await func(item);
                    results.Add(value);
                }
                catch (Exception ex)
                {
                    results.Add(ex);
                }
            }

            return results;
        }

        /// <summary>
        /// Reduces/folds a collection with a function that returns Result.
        /// Exits early on first error.
        /// </summary>
        public static Result<TAccumulate, TError> Reduce<T, TAccumulate, TError>(
            IEnumerable<T> items,
            TAccumulate seed,
            Func<TAccumulate, T, Result<TAccumulate, TError>> accumulator)
        {
            var current = seed;

            foreach (var item in items)
            {
                var result = accumulator(current, item);
                if (result.IsFailure)
                    return result.Error;

                current = result.Value;
            }

            return current;
        }

        /// <summary>
        /// Async version of Reduce.
        /// </summary>
        public static async Task<Result<TAccumulate, TError>> ReduceAsync<T, TAccumulate, TError>(
            IEnumerable<T> items,
            TAccumulate seed,
            Func<TAccumulate, T, Task<Result<TAccumulate, TError>>> accumulator)
        {
            var current = seed;

            foreach (var item in items)
            {
                var result = await accumulator(current, item);
                if (result.IsFailure)
                    return result.Error;

                current = result.Value;
            }

            return current;
        }

        /// <summary>
        /// Groups items by a key selector that returns Result.
        /// Returns error if any key selection fails.
        /// </summary>
        public static Result<Dictionary<TKey, List<T>>, TError> GroupByResult<T, TKey, TError>(
            IEnumerable<T> items,
            Func<T, Result<TKey, TError>> keySelector) where TKey : notnull
        {
            var groups = new Dictionary<TKey, List<T>>();

            foreach (var item in items)
            {
                var keyResult = keySelector(item);
                if (keyResult.IsFailure)
                    return keyResult.Error;

                var key = keyResult.Value;
                if (!groups.ContainsKey(key))
                    groups[key] = [];

                groups[key].Add(item);
            }

            return groups;
        }

        /// <summary>
        /// Flattens nested Results from a collection.
        /// Maps each result's value to a collection, then flattens.
        /// </summary>
        public static Result<List<TNew>, TError> SelectMany<T, TNew, TError>(
            IEnumerable<Result<T, TError>> results,
            Func<T, IEnumerable<TNew>> selector)
        {
            var values = new List<TNew>();

            foreach (var result in results)
            {
                if (result.IsFailure)
                    return result.Error;

                values.AddRange(selector(result.Value));
            }

            return values;
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
