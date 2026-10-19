using Mapster;
using TimeSloth.Domain.Skills;

namespace TimeSloth.Services.Skills;

public sealed class SkillMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Skill, SkillReadDto>();
    }
}
