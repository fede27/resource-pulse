using ResourcePulse.Common.Auth;

namespace ResourcePulse.SignalWorker;

/// <summary>
/// The detector's identity for auditing purposes.
/// </summary>
/// <remarks>
/// <para>
/// Rows the sweep writes have an author, and it is not a person: recording a
/// machine name is honest, whereas leaving <c>CreatedBy</c> blank or borrowing
/// somebody's subject would make the audit trail lie about who decided what.
/// </para>
/// <para>
/// It reports <c>IsAuthenticated = false</c> on purpose. Nothing in the worker
/// evaluates an authorization policy — authorization is a request-path concern —
/// and claiming authentication here would be inventing a principal that never
/// authenticated.
/// </para>
/// </remarks>
public sealed class SystemUserAccessor : ICurrentUserAccessor
{
    public const string SystemSub = "system:signal-detector";

    public CurrentUser User { get; } = new(SystemSub, string.Empty, "Signal detector", new Dictionary<string, string>());

    public bool IsAuthenticated => false;

    public string? AuthenticationScheme => null;
}
