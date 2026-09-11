using Mapster;
using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Services.ExternalConstraints;

public sealed class ExternalConstraintMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ExternalConstraint, ExternalConstraintReadDto>()
            .Ignore(d => d.AnchoredEdgeCount);
    }
}
