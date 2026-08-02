namespace ResourcePulse.Common.Auth;

public interface ICurrentUserAccessor
{
    CurrentUser User { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// The authentication scheme that produced the current principal, or null
    /// when anonymous. Needed to distinguish the local development stub from a
    /// real identity provider — claims that are acceptable from the former must
    /// not be trusted from the latter (ADR-0029).
    /// </summary>
    string? AuthenticationScheme { get; }
}
