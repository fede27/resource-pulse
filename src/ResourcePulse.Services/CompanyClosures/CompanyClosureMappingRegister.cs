using Mapster;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Services.CompanyClosures;

public sealed class CompanyClosureMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CompanyClosure, CompanyClosureReadDto>();
    }
}
