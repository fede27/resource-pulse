using Mapster;
using TimeSloth.Domain.Roles;

namespace TimeSloth.Services.Roles;

public sealed class RoleMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Role, RoleReadDto>();
    }
}
