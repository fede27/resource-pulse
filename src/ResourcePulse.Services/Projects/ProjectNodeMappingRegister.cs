using Mapster;
using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Services.Projects;

public sealed class ProjectNodeMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectSkillRequirement, ProjectSkillRequirementDto>();
        config.NewConfig<ProjectNodeTag, ProjectNodeTagDto>();
        config.NewConfig<ProjectNode, ProjectNodeReadDto>();
    }
}
