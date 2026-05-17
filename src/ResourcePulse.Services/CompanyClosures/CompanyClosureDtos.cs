namespace ResourcePulse.Services.CompanyClosures;

public sealed class CompanyClosureReadDto
{
    public Guid Id { get; init; }
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class CreateCompanyClosureDto
{
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class UpdateCompanyClosureDto
{
    public DateOnly DateFrom { get; init; }
    public DateOnly DateTo { get; init; }
    public string Reason { get; init; } = string.Empty;
}
