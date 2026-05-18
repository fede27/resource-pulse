using Mapster;
using ResourcePulse.Domain.Tags;

namespace ResourcePulse.Services.Tags;

public sealed class TagMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Tag, TagReadDto>();
    }
}
