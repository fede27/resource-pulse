using Mapster;
using TimeSloth.Domain.Tags;

namespace TimeSloth.Services.Tags;

public sealed class TagMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Tag, TagReadDto>();
    }
}
