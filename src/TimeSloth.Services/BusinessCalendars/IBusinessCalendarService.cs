using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using TimeSloth.Common.Results;
using TimeSloth.Services.Shared;

namespace TimeSloth.Services.BusinessCalendars;

public interface IBusinessCalendarService
{
    Task<ServiceResult<LoadResult>> GetAllAsync(DataSourceLoadOptionsBase? loadOptions = null, CancellationToken ct = default);
    Task<ServiceResult<BusinessCalendarReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceResult<BusinessCalendarReadDto>> CreateAsync(CreateBusinessCalendarDto dto, CancellationToken ct = default);
    Task<ServiceResult<BusinessCalendarReadDto>> UpdateAsync(Guid id, UpdateBusinessCalendarDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<WorkWindowDto>> AddWorkWindowAsync(Guid calendarId, WorkWindowDto dto, CancellationToken ct = default);
    Task<ServiceResult<Unit>> RemoveWorkWindowAsync(Guid calendarId, Guid windowId, CancellationToken ct = default);
    Task<ServiceResult<Unit>> MarkAsDefaultAsync(Guid calendarId, CancellationToken ct = default);
}
