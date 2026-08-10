using Microsoft.AspNetCore.Mvc;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// The distinct ways a request can be refused with 403, and the machine-readable
/// <c>type</c> that tells them apart.
/// </summary>
/// <remarks>
/// Two 403s that look identical on the wire are a support ticket: "your
/// organization is not mapped to a tenant" and "you do not have the role for this"
/// need different words, a different audience and a different fix. The client
/// branches on <c>type</c> — never on the human-readable title, which is display
/// copy and free to change.
/// </remarks>
public static class AuthProblems
{
    public const string TenantNotResolved = "urn:resourcepulse:problem:tenant-not-resolved";
    public const string NotAMember = "urn:resourcepulse:problem:not-a-member";
    public const string InsufficientRole = "urn:resourcepulse:problem:insufficient-role";

    public static ProblemDetails Create(string type, string title, string detail) => new()
    {
        Type = type,
        Title = title,
        Detail = detail,
        Status = StatusCodes.Status403Forbidden
    };
}
