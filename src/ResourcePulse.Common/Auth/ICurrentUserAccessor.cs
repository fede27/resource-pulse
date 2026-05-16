namespace ResourcePulse.Common.Auth;

public interface ICurrentUserAccessor
{
    CurrentUser User { get; }
    bool IsAuthenticated { get; }
}
