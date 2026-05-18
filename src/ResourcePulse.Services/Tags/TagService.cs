using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain;
using ResourcePulse.Domain.Tags;

namespace ResourcePulse.Services.Tags;

public sealed class TagService(
    IRepository<Tag, Guid> repository,
    IMapper mapper) : ITagService
{
    public async Task<ServiceResult<LoadResult>> GetAllAsync(
        DataSourceLoadOptionsBase? loadOptions = null,
        CancellationToken ct = default)
    {
        var query = repository.Query().ProjectToType<TagReadDto>();
        var result = await DataSourceLoader.LoadAsync(query, loadOptions ?? new DataSourceLoadOptionsBase(), ct);
        return ServiceResult<LoadResult>.Success(result);
    }

    public async Task<ServiceResult<TagReadDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await repository.Query()
            .Where(t => t.Id == id)
            .ProjectToType<TagReadDto>()
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? ServiceResult<TagReadDto>.NotFound($"Tag {id} not found.")
            : ServiceResult<TagReadDto>.Success(dto);
    }

    public async Task<ServiceResult<TagReadDto>> CreateAsync(CreateTagDto dto, CancellationToken ct = default)
    {
        var tag = Tag.Create(dto.Name);
        await repository.AddAsync(tag, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<TagReadDto>.Conflict($"A tag named '{tag.Name}' already exists.");
        }

        return ServiceResult<TagReadDto>.Success(mapper.Map<TagReadDto>(tag));
    }

    public async Task<ServiceResult<TagReadDto>> UpdateAsync(Guid id, UpdateTagDto dto, CancellationToken ct = default)
    {
        var tag = await repository.GetByIdAsync(id, ct);
        if (tag is null) return ServiceResult<TagReadDto>.NotFound($"Tag {id} not found.");

        tag.Rename(dto.Name);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ServiceResult<TagReadDto>.Conflict($"A tag named '{tag.Name}' already exists.");
        }

        return ServiceResult<TagReadDto>.Success(mapper.Map<TagReadDto>(tag));
    }

    public async Task<ServiceResult<Unit>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await repository.GetByIdAsync(id, ct);
        if (tag is null) return ServiceResult.NotFound($"Tag {id} not found.");

        // Join tables (ResourceTag, ProjectNodeTag) cascade on tag deletion — no FK conflict expected.
        repository.Remove(tag);
        await repository.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
