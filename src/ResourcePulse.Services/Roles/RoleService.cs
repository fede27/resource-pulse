using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Persistence;

namespace ResourcePulse.Services.Roles;

public sealed class RoleService(
    IRepository<Role, Guid> repository,
    ResourcePulseDbContext db,
    IMapper mapper) : IRoleService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<RoleReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<RoleReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(r => r.Id == id)
            .ProjectToType<RoleReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<RoleReadDto>.NotFound($"Role {id} not found.")
            : ServiceResult<RoleReadDto>.Success(dto);
    }

    public async Task<ServiceResult<RoleReadDto>> CreateAsync(CreateRoleDto dto, CancellationToken ct = default)
    {
        var role = Role.Create(dto.Name);
        await repository.AddAsync(role, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ServiceResult<RoleReadDto>.Conflict($"A role named '{role.Name}' already exists.");
        }

        return ServiceResult<RoleReadDto>.Success(mapper.Map<RoleReadDto>(role));
    }

    public async Task<ServiceResult<RoleReadDto>> UpdateAsync(Guid id, UpdateRoleDto dto, CancellationToken ct = default)
    {
        var role = await repository.GetByIdAsync(id, ct);
        if (role is null) return ServiceResult<RoleReadDto>.NotFound($"Role {id} not found.");

        role.Rename(dto.Name);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ServiceResult<RoleReadDto>.Conflict($"A role named '{role.Name}' already exists.");
        }

        return ServiceResult<RoleReadDto>.Success(mapper.Map<RoleReadDto>(role));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await repository.GetByIdAsync(id, ct);
        if (role is null) return ServiceResult.NotFound($"Role {id} not found.");

        // People and demands both hold a role by Restrict FK. Counted first, so
        // the refusal says what is in the way (ADR-0037 RoleInUse); the catch
        // below stays for what attaches in between.
        var resources = await db.Resources.CountAsync(r => r.RoleId == id, ct);
        var demands = await db.Demands.CountAsync(d => d.RoleId == id, ct);
        if (resources > 0 || demands > 0)
            return InUse(role, resources, demands);

        repository.Remove(role);
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            db.ChangeTracker.Clear();
            return InUse(role,
                await db.Resources.CountAsync(r => r.RoleId == id, ct),
                await db.Demands.CountAsync(d => d.RoleId == id, ct));
        }
        return ServiceResult.Ok();
    }

    private static ServiceResult<Unit> InUse(Role role, int resources, int demands) =>
        ServiceResult.Failure(ServiceError.Conflict(
            ProblemCode.RoleInUse,
            $"Role '{role.Name}' is held by {resources} resource(s) and {demands} demand(s) and cannot be deleted.",
            new Dictionary<string, object?>
            {
                ["role"] = role.Name,
                ["resources"] = resources,
                ["demands"] = demands,
            }));
}
