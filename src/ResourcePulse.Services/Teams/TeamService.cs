using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Teams;

namespace ResourcePulse.Services.Teams;

public sealed class TeamService(
    IRepository<Team, Guid> repository,
    IMapper mapper) : ITeamService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<TeamReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<TeamReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(t => t.Id == id)
            .ProjectToType<TeamReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<TeamReadDto>.NotFound($"Team {id} not found.")
            : ServiceResult<TeamReadDto>.Success(dto);
    }

    public async Task<ServiceResult<TeamReadDto>> CreateAsync(CreateTeamDto dto, CancellationToken ct = default)
    {
        var team = Team.Create(dto.Name);
        await repository.AddAsync(team, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<TeamReadDto>.Conflict($"A team named '{team.Name}' already exists.");
        }

        return ServiceResult<TeamReadDto>.Success(mapper.Map<TeamReadDto>(team));
    }

    public async Task<ServiceResult<TeamReadDto>> UpdateAsync(Guid id, UpdateTeamDto dto, CancellationToken ct = default)
    {
        var team = await repository.GetByIdAsync(id, ct);
        if (team is null) return ServiceResult<TeamReadDto>.NotFound($"Team {id} not found.");

        team.Rename(dto.Name);
        if (dto.IsActive) team.Activate(); else team.Deactivate();

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<TeamReadDto>.Conflict($"A team named '{team.Name}' already exists.");
        }

        return ServiceResult<TeamReadDto>.Success(mapper.Map<TeamReadDto>(team));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var team = await repository.GetByIdAsync(id, ct);
        if (team is null) return ServiceResult.NotFound($"Team {id} not found.");

        try
        {
            repository.Remove(team);
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            return ServiceResult.Conflict("Team is referenced by one or more resources.");
        }

        return ServiceResult.Ok();
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsForeignKeyViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) == true;
}
