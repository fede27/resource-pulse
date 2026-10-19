using TimeSloth.Common.Results;

namespace TimeSloth.Common.Domain;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    // A rule a client needs to tell apart (ADR-0037): the code and the pieces
    // travel with the exception, so a service that catches it and returns a
    // Conflict keeps them — see ServiceError.Conflict(DomainException).
    public DomainException(ProblemCode code, string message, IReadOnlyDictionary<string, object?>? args = null)
        : base(message)
    {
        Code = code;
        Args = args;
    }

    public ProblemCode? Code { get; }

    public IReadOnlyDictionary<string, object?>? Args { get; }
}
