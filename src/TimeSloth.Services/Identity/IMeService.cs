using TimeSloth.Common.Results;

namespace TimeSloth.Services.Identity;

public interface IMeService
{
    // Resolves the calling user's identity + linked resource + staffing-manager
    // flag (gap #8 / ADR-0024).
    Task<ServiceResult<MeDto>> GetAsync(CancellationToken ct = default);
}
