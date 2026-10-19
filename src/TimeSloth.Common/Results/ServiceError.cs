using TimeSloth.Common.Domain;

namespace TimeSloth.Common.Results;

public enum ServiceErrorKind
{
    Failure = 0,
    NotFound,
    Validation,
    Conflict,
    Forbidden
}

// `Kind` is the HTTP status; `Code` (optional, ADR-0037) is WHICH rule refused,
// and `Args` are its pieces in wire format. `Message` is English, for logs and
// for a client that does not know the code — never the user's sentence.
public sealed record ServiceError(
    ServiceErrorKind Kind,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null,
    ProblemCode? Code = null,
    IReadOnlyDictionary<string, object?>? Args = null)
{
    public static ServiceError NotFound(string message) =>
        new(ServiceErrorKind.NotFound, message);

    public static ServiceError Validation(IDictionary<string, string[]> errors) =>
        new(ServiceErrorKind.Validation, "Validation failed.",
            new Dictionary<string, string[]>(errors));

    public static ServiceError Conflict(string message) =>
        new(ServiceErrorKind.Conflict, message);

    public static ServiceError Conflict(ProblemCode code, string message, IReadOnlyDictionary<string, object?> args) =>
        new(ServiceErrorKind.Conflict, message, Code: code, Args: args);

    // The documented translation of a domain rule into a 409 — keeping its code,
    // so a rule the client tells apart stays tellable once a service catches it.
    public static ServiceError Conflict(DomainException exception) =>
        new(ServiceErrorKind.Conflict, exception.Message, Code: exception.Code, Args: exception.Args);

    public static ServiceError Forbidden(string message) =>
        new(ServiceErrorKind.Forbidden, message);

    public static ServiceError Failure(string message) =>
        new(ServiceErrorKind.Failure, message);
}
