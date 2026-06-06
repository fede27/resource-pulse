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
        RuleFor(x => x.Status).IsInEnum();
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
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreatePlaceholderByPercentDtoValidator : AbstractValidator<CreatePlaceholderByPercentDto>
{
    public CreatePlaceholderByPercentDtoValidator()
    {
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.Percent)
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"Percent must be in the range (0, {Allocation.MaxAllocationPercent}].");
        RuleFor(x => x.RoleSkillId).NotEqual(Guid.Empty);
        RuleFor(x => x.OwnerResourceId)
            .Must(o => o is null || o != Guid.Empty)
            .WithMessage("OwnerResourceId, when provided, must not be Guid.Empty.");
        RuleFor(x => x.Status).IsInEnum();
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

public sealed class ConvertToPlaceholderDtoValidator : AbstractValidator<ConvertToPlaceholderDto>
{
    public ConvertToPlaceholderDtoValidator()
    {
        RuleFor(x => x.RoleSkillId).NotEqual(Guid.Empty);
        RuleFor(x => x.OwnerResourceId)
            .Must(o => o is null || o != Guid.Empty)
            .WithMessage("OwnerResourceId, when provided, must not be Guid.Empty.");
    }
}

public sealed class AssignToResourceDtoValidator : AbstractValidator<AssignToResourceDto>
{
    public AssignToResourceDtoValidator()
    {
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
    }
}

public sealed class ChangeAllocationStatusDtoValidator : AbstractValidator<ChangeAllocationStatusDto>
{
    public ChangeAllocationStatusDtoValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
