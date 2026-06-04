using Mapster;
using ResourcePulse.Domain.Roles;

namespace ResourcePulse.Services.Roles;

public sealed class RoleMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Role, RoleReadDto>();
    }
}
