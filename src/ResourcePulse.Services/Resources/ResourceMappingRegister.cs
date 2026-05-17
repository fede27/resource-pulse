using Mapster;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Services.Resources;

public sealed class ResourceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<IndividualAdjustment, IndividualAdjustmentDto>();
        config.NewConfig<Resource, ResourceReadDto>();
    }
}
