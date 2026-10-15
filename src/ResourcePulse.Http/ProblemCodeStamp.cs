using Microsoft.AspNetCore.Mvc;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Http;

/// <summary>
/// The one place a <see cref="ProblemCode"/> is written onto a ProblemDetails
/// (ADR-0037): the code as the <c>type</c>, its pieces as the <c>args</c>
/// extension. Both producers of refusals — the controllers, through
/// <see cref="ControllerFoundation"/>, and the exception handler for an uncaught
/// <c>DomainException</c> — go through it, so the two cannot drift apart.
/// </summary>
public static class ProblemCodeStamp
{
    public const string ArgsExtension = "args";

    public static ProblemDetails Apply(ProblemDetails problem, ProblemCode? code, IReadOnlyDictionary<string, object?>? args)
    {
        if (code is not { } c) return problem;
        problem.Type = ProblemCodes.TypeOf(c);
        // Always present when there is a code: the client reads `args.x`, and a
        // missing object would be one more shape to guard against.
        problem.Extensions[ArgsExtension] = args ?? new Dictionary<string, object?>();
        return problem;
    }
}
