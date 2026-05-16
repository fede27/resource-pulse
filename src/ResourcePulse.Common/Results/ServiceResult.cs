namespace ResourcePulse.Common.Results;

public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

public readonly record struct ServiceResult<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }
    public ServiceError? Error { get; }

    private ServiceResult(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private ServiceResult(ServiceError error)
    {
        IsSuccess = false;
        Value = default!;
        Error = error;
    }

    public static ServiceResult<T> Success(T value) => new(value);
    public static ServiceResult<T> NotFound(string message) => new(ServiceError.NotFound(message));
    public static ServiceResult<T> Validation(IDictionary<string, string[]> errors) => new(ServiceError.Validation(errors));
    public static ServiceResult<T> Conflict(string message) => new(ServiceError.Conflict(message));
    public static ServiceResult<T> Forbidden(string message) => new(ServiceError.Forbidden(message));
    public static ServiceResult<T> Failure(ServiceError error) => new(error);
}

public static class ServiceResult
{
    public static ServiceResult<Unit> Ok() => ServiceResult<Unit>.Success(Unit.Value);
    public static ServiceResult<Unit> NotFound(string message) => ServiceResult<Unit>.NotFound(message);
    public static ServiceResult<Unit> Validation(IDictionary<string, string[]> errors) => ServiceResult<Unit>.Validation(errors);
    public static ServiceResult<Unit> Conflict(string message) => ServiceResult<Unit>.Conflict(message);
    public static ServiceResult<Unit> Forbidden(string message) => ServiceResult<Unit>.Forbidden(message);
    public static ServiceResult<Unit> Failure(ServiceError error) => ServiceResult<Unit>.Failure(error);
}
