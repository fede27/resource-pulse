using Mapster;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Services.Skills;

public sealed class SkillMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Skill, SkillReadDto>();
    }
}
