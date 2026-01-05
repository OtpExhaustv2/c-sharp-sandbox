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
    }

}

