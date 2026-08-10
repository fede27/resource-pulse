using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Access;

namespace ResourcePulse.Services.Access;

/// <summary>
/// Development-only: lets a developer act as any role without a second account.
/// </summary>
/// <remarks>
/// It changes the caller's <b>real</b> membership row, which is what makes it
/// worth having: the request path afterwards is the production one — same
/// resolver, same policies, same <c>/api/me</c>, same UI gating. A configuration
/// switch or a trusted header would each bypass the very mechanism being
/// exercised, and would leave dev testing an arrangement that does not exist in
/// production.
/// </remarks>
public interface IDevAccessService
{
    Task<ServiceResult<AppRole>> ActAsAsync(AppRole role, CancellationToken ct = default);
}

public sealed class DevActAsDto
{
    public AppRole Role { get; init; }
}
