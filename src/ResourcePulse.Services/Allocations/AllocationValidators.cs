using FluentValidation;

namespace ResourcePulse.Services.Allocations;

public sealed class CreateAllocationDtoValidator : AbstractValidator<CreateAllocationDto>
{
    public CreateAllocationDtoValidator()
    {
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.AllocationPercent)
            .GreaterThan(0m).LessThanOrEqualTo(100m)
            .WithMessage("AllocationPercent must be in the range (0, 100].");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateAllocationDtoValidator : AbstractValidator<UpdateAllocationDto>
{
    public UpdateAllocationDtoValidator()
    {
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.AllocationPercent)
            .GreaterThan(0m).LessThanOrEqualTo(100m)
            .WithMessage("AllocationPercent must be in the range (0, 100].");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
