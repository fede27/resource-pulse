using Mapster;
using TimeSloth.Domain.Calendars;

namespace TimeSloth.Services.Shared;

public sealed class SharedMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<WorkWindow, WorkWindowDto>();
    }
}
