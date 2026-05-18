using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Services.Skills;

public sealed class SkillService(
    IRepository<Skill, Guid> repository,
    IMapper mapper) : ISkillService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<SkillReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<SkillReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(s => s.Id == id)
            .ProjectToType<SkillReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<SkillReadDto>.NotFound($"Skill {id} not found.")
            : ServiceResult<SkillReadDto>.Success(dto);
    }

    public async Task<ServiceResult<SkillReadDto>> CreateAsync(CreateSkillDto dto, CancellationToken ct = default)
    {
        var skill = Skill.Create(dto.Name, dto.Category);
        await repository.AddAsync(skill, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<SkillReadDto>.Conflict($"A skill named '{skill.Name}' already exists.");
        }

        return ServiceResult<SkillReadDto>.Success(mapper.Map<SkillReadDto>(skill));
    }

    public async Task<ServiceResult<SkillReadDto>> UpdateAsync(Guid id, UpdateSkillDto dto, CancellationToken ct = default)
    {
        var skill = await repository.GetByIdAsync(id, ct);
        if (skill is null) return ServiceResult<SkillReadDto>.NotFound($"Skill {id} not found.");

        skill.Rename(dto.Name);
        skill.ChangeCategory(dto.Category);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<SkillReadDto>.Conflict($"A skill named '{skill.Name}' already exists.");
        }

        return ServiceResult<SkillReadDto>.Success(mapper.Map<SkillReadDto>(skill));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var skill = await repository.GetByIdAsync(id, ct);
        if (skill is null) return ServiceResult.NotFound($"Skill {id} not found.");

        try
        {
            repository.Remove(skill);
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            return ServiceResult.Conflict("Skill is referenced by one or more resources or projects.");
        }

        return ServiceResult.Ok();
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsForeignKeyViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) == true;
}
