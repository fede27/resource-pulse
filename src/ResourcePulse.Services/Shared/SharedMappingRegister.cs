using Mapster;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Services.Shared;

public sealed class SharedMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<WorkWindow, WorkWindowDto>();
    }
}
