using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Mvc;

namespace ResourcePulse.Http;

public abstract class ReadOnlyController<TDto, TKey> : ControllerFoundation
{
    [HttpGet("{id}")]
    public abstract Task<IActionResult> GetByIdAsync(TKey id, CancellationToken ct);

    [HttpGet]
    public abstract Task<IActionResult> GetAllAsync(DataSourceLoadOptionsBase? loadOptions, CancellationToken ct);
}
