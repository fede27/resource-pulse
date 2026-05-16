using Mapster;
using ResourcePulse.Domain;

namespace ResourcePulse.Services.Ping;

public sealed class PingMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Domain.Ping, PingDto>();
    }
}
