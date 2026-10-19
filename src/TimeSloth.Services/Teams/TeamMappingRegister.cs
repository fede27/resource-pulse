using Mapster;
using TimeSloth.Domain.Teams;

namespace TimeSloth.Services.Teams;

public sealed class TeamMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Team, TeamReadDto>();
    }
}
