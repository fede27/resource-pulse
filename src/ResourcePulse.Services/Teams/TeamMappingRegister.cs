using Mapster;
using ResourcePulse.Domain.Teams;

namespace ResourcePulse.Services.Teams;

public sealed class TeamMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Team, TeamReadDto>();
    }
}
