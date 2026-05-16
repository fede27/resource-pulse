using Microsoft.AspNetCore.Mvc;

namespace ResourcePulse.Http;

public abstract class CrudController<TCreateDto, TUpdateDto, TReadDto, TKey>
    : ReadOnlyController<TReadDto, TKey>
{
    [HttpPost]
    public abstract Task<IActionResult> CreateAsync([FromBody] TCreateDto dto, CancellationToken ct);

    [HttpPut("{id}")]
    public abstract Task<IActionResult> UpdateAsync(TKey id, [FromBody] TUpdateDto dto, CancellationToken ct);

    [HttpDelete("{id}")]
    public abstract Task<IActionResult> DeleteAsync(TKey id, CancellationToken ct);
}
