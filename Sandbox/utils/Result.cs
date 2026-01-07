using System.Diagnostics.CodeAnalysis;

namespace Sandbox.utils
{
    public readonly struct Result<T, TError> : IEquatable<Result<T, TError>>
    {
        private readonly T? _value;
        private readonly TError? _error;
        private readonly bool _isSuccess;

        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess => _isSuccess;

        [MemberNotNullWhen(true, nameof(Error))]
        [MemberNotNullWhen(false, nameof(Value))]
        public bool IsFailure => !_isSuccess;

        public T Value => _isSuccess
           ? _value!
           : throw new InvalidOperationException("Cannot access Value on a failed result.");

        public TError Error => !_isSuccess
            ? _error!
            : throw new InvalidOperationException("Cannot access Error on a successful result.");

        private Result(T value)
        {
            _value = value;
            _error = default;
            _isSuccess = true;
        }

        private Result(TError error)
        {
            _value = default;
            _error = error;
            _isSuccess = false;
        }

        public static Result<T, TError> Success(T value) => new(value);

        public static Result<T, TError> Failure(TError error) => new(error);

        public static implicit operator Result<T, TError>(T value) => Success(value);
        public static implicit operator Result<T, TError>(TError error) => Failure(error);

        public static explicit operator T(Result<T, TError> result) => result.Value;
        public static explicit operator TError(Result<T, TError> result) => result.Error;

        public Result<T, TError> Match(Action<T> onSuccess, Action<TError> onFailure)
        {
            if (_isSuccess)
                onSuccess(_value!);
            else
                onFailure(_error!);

            return this;
        }

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onFailure)
        {
            return _isSuccess ? onSuccess(_value!) : onFailure(_error!);
        }

        public Result<TNew, TError> Map<TNew>(Func<T, TNew> mapper)
        {
            return _isSuccess
                ? Result<TNew, TError>.Success(mapper(_value!))
                : Result<TNew, TError>.Failure(_error!);
        }

        public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> mapper)
        {
            return _isSuccess
                ? Result<T, TNewError>.Success(_value!)
                : Result<T, TNewError>.Failure(mapper(_error!));
        }

        public Result<TNew, TError> Bind<TNew>(Func<T, Result<TNew, TError>> binder)
        {
            return _isSuccess ? binder(_value!) : Result<TNew, TError>.Failure(_error!);
        }

        public async Task<Result<TNew, TError>> BindAsync<TNew>(Func<T, Task<Result<TNew, TError>>> binder)
        {
            return _isSuccess ? await binder(_value!) : Result<TNew, TError>.Failure(_error!);
        }

        public Result<T, TError> OnSuccess(Action<T> action)
        {
            if (_isSuccess)
                action(_value!);
            return this;
        }

        public Result<T, TError> OnFailure(Action<TError> action)
        {
            if (!_isSuccess)
                action(_error!);
            return this;
        }

        public async Task<Result<T, TError>> OnSuccessAsync(Func<T, Task> action)
        {
            if (_isSuccess)
                await action(_value!);
            return this;
        }

        public async Task<Result<T, TError>> OnFailureAsync(Func<TError, Task> action)
        {
            if (!_isSuccess)
                await action(_error!);
            return this;
        }

        public T ValueOr(T defaultValue) => _isSuccess ? _value! : defaultValue;

        public T ValueOr(Func<T> defaultValueProvider) => _isSuccess ? _value! : defaultValueProvider();

        public T ValueOr(Func<TError, T> errorHandler) => _isSuccess ? _value! : errorHandler(_error!);

        public bool TryGetValue([NotNullWhen(true)] out T? value)
        {
            value = _value!;
            return _isSuccess;
        }

        public bool TryGetError([NotNullWhen(true)] out TError? error)
        {
            error = _error!;
            return !_isSuccess;
        }

        // ============================================
        // ASYNC MAPPING
        // ============================================

        /// <summary>
        /// Maps the success value asynchronously to a new value.
        /// </summary>
        public async Task<Result<TNew, TError>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
        {
            return _isSuccess
                ? Result<TNew, TError>.Success(await mapper(_value!))
                : Result<TNew, TError>.Failure(_error!);
        }

        /// <summary>
        /// Maps the error asynchronously to a new error type.
        /// </summary>
        public async Task<Result<T, TNewError>> MapErrorAsync<TNewError>(Func<TError, Task<TNewError>> mapper)
        {
            return _isSuccess
                ? Result<T, TNewError>.Success(_value!)
                : Result<T, TNewError>.Failure(await mapper(_error!));
        }

        // ============================================
        // TAP/INSPECT (Side effects)
        // ============================================

        /// <summary>
        /// Performs a side effect on the success value without changing the result.
        /// Alias for OnSuccess with more functional naming.
        /// </summary>
        public Result<T, TError> Tap(Action<T> action)
        {
            if (_isSuccess)
                action(_value!);
            return this;
        }

        /// <summary>
        /// Performs a side effect on the error without changing the result.
        /// Alias for OnFailure with more functional naming.
        /// </summary>
        public Result<T, TError> TapError(Action<TError> action)
        {
            if (!_isSuccess)
                action(_error!);
            return this;
        }

        /// <summary>
        /// Performs an async side effect on the success value without changing the result.
        /// </summary>
        public async Task<Result<T, TError>> TapAsync(Func<T, Task> action)
        {
            if (_isSuccess)
                await action(_value!);
            return this;
        }

        /// <summary>
        /// Performs an async side effect on the error without changing the result.
        /// </summary>
        public async Task<Result<T, TError>> TapErrorAsync(Func<TError, Task> action)
        {
            if (!_isSuccess)
                await action(_error!);
            return this;
        }

        // ============================================
        // ENSURE/FILTER (Validation)
        // ============================================

        /// <summary>
        /// Ensures that the success value satisfies a predicate, or converts to failure.
        /// </summary>
        public Result<T, TError> Ensure(Func<T, bool> predicate, Func<T, TError> errorFactory)
        {
            return _isSuccess && !predicate(_value!)
                ? Failure(errorFactory(_value!))
                : this;
        }

        /// <summary>
        /// Ensures that the success value satisfies a predicate with a fixed error.
        /// </summary>
        public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
        {
            return _isSuccess && !predicate(_value!)
                ? Failure(error)
                : this;
        }

        /// <summary>
        /// Async version of Ensure.
        /// </summary>
        public async Task<Result<T, TError>> EnsureAsync(
            Func<T, Task<bool>> predicate,
            Func<T, TError> errorFactory)
        {
            if (_isSuccess && !await predicate(_value!))
                return Failure(errorFactory(_value!));
            return this;
        }

        // ============================================
        // RECOVER/ORELSE (Error recovery)
        // ============================================

        /// <summary>
        /// Provides a fallback result if this result is a failure.
        /// </summary>
        public Result<T, TError> OrElse(Func<TError, Result<T, TError>> fallback)
        {
            return _isSuccess ? this : fallback(_error!);
        }

        public Result<T, TError> OrElse(Result<T, TError> fallback)
        {
            return _isSuccess ? this : fallback;
        }

        /// <summary>
        /// Recovers from failure by converting the error to a success value.
        /// </summary>
        public Result<T, TError> Recover(Func<TError, T> recovery)
        {
            return _isSuccess ? this : Success(recovery(_error!));
        }

        /// <summary>
        /// Async version of OrElse.
        /// </summary>
        public async Task<Result<T, TError>> OrElseAsync(Func<TError, Task<Result<T, TError>>> fallback)
        {
            return _isSuccess ? this : await fallback(_error!);
        }

        /// <summary>
        /// Async version of Recover.
        /// </summary>
        public async Task<Result<T, TError>> RecoverAsync(Func<TError, Task<T>> recovery)
        {
            return _isSuccess ? this : Success(await recovery(_error!));
        }

        // ============================================
        // ZIP/MERGE (Combine two Results)
        // ============================================

        /// <summary>
        /// Combines two Results using a zipper function.
        /// Returns failure if either result is a failure.
        /// </summary>
        public Result<TNew, TError> Zip<TOther, TNew>(
            Result<TOther, TError> other,
            Func<T, TOther, TNew> zipper)
        {
            return _isSuccess && other.IsSuccess
                ? Result<TNew, TError>.Success(zipper(_value!, other.Value))
                : Result<TNew, TError>.Failure(_isSuccess ? other.Error : _error!);
        }

        /// <summary>
        /// Combines this result with another, keeping only the second value if both succeed.
        /// </summary>
        public Result<TOther, TError> ZipRight<TOther>(Result<TOther, TError> other)
        {
            return _isSuccess && other.IsSuccess
                ? other
                : Result<TOther, TError>.Failure(_isSuccess ? other.Error : _error!);
        }

        /// <summary>
        /// Combines this result with another, keeping only the first value if both succeed.
        /// </summary>
        public Result<T, TError> ZipLeft<TOther>(Result<TOther, TError> other)
        {
            return _isSuccess && other.IsSuccess
                ? this
                : Failure(_isSuccess ? other.Error : _error!);
        }

        // ============================================
        // BIMAP (Transform both paths)
        // ============================================

        /// <summary>
        /// Maps both success and error values simultaneously.
        /// </summary>
        public Result<TNew, TNewError> BiMap<TNew, TNewError>(
            Func<T, TNew> successMapper,
            Func<TError, TNewError> errorMapper)
        {
            return _isSuccess
                ? Result<TNew, TNewError>.Success(successMapper(_value!))
                : Result<TNew, TNewError>.Failure(errorMapper(_error!));
        }

        // ============================================
        // CONVERSIONS
        // ============================================

        /// <summary>
        /// Converts success value to nullable. Returns null if failure.
        /// </summary>
        public T? ToNullable()
        {
            return _isSuccess ? _value : default;
        }

        public override bool Equals(object? obj)
        {
            return obj is Result<T, TError> result && Equals(result);
        }

        public bool Equals(Result<T, TError> other)
        {
            if (_isSuccess != other._isSuccess)
                return false;

            return _isSuccess
                ? EqualityComparer<T>.Default.Equals(_value!, other._value!)
                : EqualityComparer<TError>.Default.Equals(_error!, other._error!);
        }

        public override int GetHashCode()
        {
            return _isSuccess
                ? HashCode.Combine(_isSuccess, _value!)
                : HashCode.Combine(_isSuccess, _error!);
        }

        public static bool operator ==(Result<T, TError> left, Result<T, TError> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Result<T, TError> left, Result<T, TError> right)
        {
            return !left.Equals(right);
        }
    }

    public static class ResultExtensions
    {
        // ============================================
        // TASK<RESULT> EXTENSIONS - Existing
        // ============================================

        public static async Task<Result<TNew, TError>> MapAsync<T, TNew, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, TNew> mapper)
        {
            var result = await resultTask;
            return result.Map(mapper);
        }

        public static async Task<Result<TNew, TError>> BindAsync<T, TNew, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, Result<TNew, TError>> binder)
        {
            var result = await resultTask;
            return result.Bind(binder);
        }

        public static async Task<Result<TNew, TError>> BindAsync<T, TNew, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, Task<Result<TNew, TError>>> binder)
        {
            var result = await resultTask;
            return await result.BindAsync(binder);
        }

        public static async Task<Result<T, TError>> OnSuccessAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Action<T> action)
        {
            var result = await resultTask;
            return result.OnSuccess(action);
        }

        public static async Task<Result<T, TError>> OnFailureAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Action<TError> action)
        {
            var result = await resultTask;
            return result.OnFailure(action);
        }

        // ============================================
        // TASK<RESULT> EXTENSIONS - New
        // ============================================

        /// <summary>
        /// Maps the success value asynchronously (awaits Task, then maps with async mapper).
        /// </summary>


        /// <summary>
        /// Maps the error asynchronously.
        /// </summary>
        public static async Task<Result<T, TNewError>> MapErrorAsync<T, TError, TNewError>(
            this Task<Result<T, TError>> resultTask,
            Func<TError, TNewError> mapper)
        {
            var result = await resultTask;
            return result.MapError(mapper);
        }

        /// <summary>
        /// Taps into the success value (side effect).
        /// </summary>
        public static async Task<Result<T, TError>> TapAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Action<T> action)
        {
            var result = await resultTask;
            return result.Tap(action);
        }

        /// <summary>
        /// Taps into the success value with async action.
        /// </summary>
        public static async Task<Result<T, TError>> TapAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, Task> action)
        {
            var result = await resultTask;
            return await result.TapAsync(action);
        }

        /// <summary>
        /// Taps into the error.
        /// </summary>
        public static async Task<Result<T, TError>> TapErrorAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Action<TError> action)
        {
            var result = await resultTask;
            return result.TapError(action);
        }

        /// <summary>
        /// Ensures predicate on Task result.
        /// </summary>
        public static async Task<Result<T, TError>> EnsureAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, bool> predicate,
            Func<T, TError> errorFactory)
        {
            var result = await resultTask;
            return result.Ensure(predicate, errorFactory);
        }

        /// <summary>
        /// Ensures async predicate on Task result.
        /// </summary>
        public static async Task<Result<T, TError>> EnsureAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, Task<bool>> predicate,
            Func<T, TError> errorFactory)
        {
            var result = await resultTask;
            return await result.EnsureAsync(predicate, errorFactory);
        }

        /// <summary>
        /// Provides fallback for Task result.
        /// </summary>
        public static async Task<Result<T, TError>> OrElseAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<TError, Result<T, TError>> fallback)
        {
            var result = await resultTask;
            return result.OrElse(fallback);
        }

        /// <summary>
        /// Provides async fallback for Task result.
        /// </summary>
        public static async Task<Result<T, TError>> OrElseAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<TError, Task<Result<T, TError>>> fallback)
        {
            var result = await resultTask;
            return await result.OrElseAsync(fallback);
        }

        /// <summary>
        /// Recovers from error.
        /// </summary>
        public static async Task<Result<T, TError>> RecoverAsync<T, TError>(
            this Task<Result<T, TError>> resultTask,
            Func<TError, T> recovery)
        {
            var result = await resultTask;
            return result.Recover(recovery);
        }

        /// <summary>
        /// Zips two Task results together.
        /// </summary>
        public static async Task<Result<TNew, TError>> ZipAsync<T, TOther, TNew, TError>(
            this Task<Result<T, TError>> resultTask,
            Task<Result<TOther, TError>> otherTask,
            Func<T, TOther, TNew> zipper)
        {
            var result = await resultTask;
            var other = await otherTask;
            return result.Zip(other, zipper);
        }

        /// <summary>
        /// BiMaps on Task result.
        /// </summary>
        public static async Task<Result<TNew, TNewError>> BiMapAsync<T, TError, TNew, TNewError>(
            this Task<Result<T, TError>> resultTask,
            Func<T, TNew> successMapper,
            Func<TError, TNewError> errorMapper)
        {
            var result = await resultTask;
            return result.BiMap(successMapper, errorMapper);
        }
    }

}

