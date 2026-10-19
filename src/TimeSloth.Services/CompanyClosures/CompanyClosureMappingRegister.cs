using Mapster;
using TimeSloth.Domain.Calendars;

namespace TimeSloth.Services.CompanyClosures;

public sealed class CompanyClosureMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CompanyClosure, CompanyClosureReadDto>();
    }
}
