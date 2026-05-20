using FluentValidation;
using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Allocations;

public sealed class CreateByPercentDtoValidator : AbstractValidator<CreateByPercentDto>
{
    public CreateByPercentDtoValidator()
    {
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.Percent)
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"Percent must be in the range (0, {Allocation.MaxAllocationPercent}].");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateByHoursDtoValidator : AbstractValidator<CreateByHoursDto>
{
    public CreateByHoursDtoValidator()
    {
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.TargetHours).GreaterThan(TimeSpan.Zero)
            .WithMessage("TargetHours must be greater than zero.");
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
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"AllocationPercent must be in the range (0, {Allocation.MaxAllocationPercent}].");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class MoveAllocationDtoValidator : AbstractValidator<MoveAllocationDto>
{
    public MoveAllocationDtoValidator()
    {
        RuleFor(x => x.NewPeriodStart).LessThanOrEqualTo(x => x.NewPeriodEnd)
            .WithMessage("NewPeriodStart must be on or before NewPeriodEnd.");
        RuleFor(x => x.Mode).IsInEnum();
    }
}
