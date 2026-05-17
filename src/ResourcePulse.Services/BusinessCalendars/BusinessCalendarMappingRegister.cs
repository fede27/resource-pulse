using Mapster;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Services.BusinessCalendars;

public sealed class BusinessCalendarMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<BusinessCalendar, BusinessCalendarReadDto>();
    }
}
