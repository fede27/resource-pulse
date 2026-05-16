namespace ResourcePulse.Common.Results;

public enum ServiceErrorKind
{
    Failure = 0,
    NotFound,
    Validation,
    Conflict,
    Forbidden
}

public sealed record ServiceError(
    ServiceErrorKind Kind,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static ServiceError NotFound(string message) =>
        new(ServiceErrorKind.NotFound, message);

    public static ServiceError Validation(IDictionary<string, string[]> errors) =>
        new(ServiceErrorKind.Validation, "Validation failed.",
            new Dictionary<string, string[]>(errors));

    public static ServiceError Conflict(string message) =>
        new(ServiceErrorKind.Conflict, message);

    public static ServiceError Forbidden(string message) =>
        new(ServiceErrorKind.Forbidden, message);

    public static ServiceError Failure(string message) =>
        new(ServiceErrorKind.Failure, message);
}
