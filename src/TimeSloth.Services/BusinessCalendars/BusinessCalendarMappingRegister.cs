using Mapster;
using TimeSloth.Domain.Calendars;

namespace TimeSloth.Services.BusinessCalendars;

public sealed class BusinessCalendarMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<BusinessCalendar, BusinessCalendarReadDto>();
    }
}
