using FluentValidation;
using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Services.Plan;

// One validator per command branch. The DtoValidationFilter resolves the
// validator by the runtime type of the polymorphic body (argument.GetType()),
// so these are picked up automatically for POST /api/plan/commands.
//
// Boundary checks only. Span-relative rules (split interiority, resize-produces
// start<=end against the live span) are domain invariants, enforced in the
// aggregate and surfaced as Conflict.

public sealed class CreateCommandValidator : AbstractValidator<CreateCommand>
{
    public CreateCommandValidator()
    {
        RuleFor(x => x.DemandId).NotEqual(Guid.Empty);
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.Percent)
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"Percent must be in the range (0, {Allocation.MaxAllocationPercent}].");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.StartAnchor!).SetValidator(new AnchorSpecValidator()).When(x => x.StartAnchor is not null);
        RuleFor(x => x.EndAnchor!).SetValidator(new AnchorSpecValidator()).When(x => x.EndAnchor is not null);
    }
}

public sealed class CreateByHoursCommandValidator : AbstractValidator<CreateByHoursCommand>
{
    public CreateByHoursCommandValidator()
    {
        RuleFor(x => x.DemandId).NotEqual(Guid.Empty);
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.TargetHours).GreaterThan(TimeSpan.Zero)
            .WithMessage("TargetHours must be greater than zero.");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.StartAnchor!).SetValidator(new AnchorSpecValidator()).When(x => x.StartAnchor is not null);
        RuleFor(x => x.EndAnchor!).SetValidator(new AnchorSpecValidator()).When(x => x.EndAnchor is not null);
    }
}

public sealed class CoverInferredCommandValidator : AbstractValidator<CoverInferredCommand>
{
    public CoverInferredCommandValidator()
    {
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.RoleId).NotEqual(Guid.Empty);
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.Percent)
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"Percent must be in the range (0, {Allocation.MaxAllocationPercent}].");
        RuleFor(x => x.OwnerResourceId)
            .Must(o => o is null || o != Guid.Empty)
            .WithMessage("OwnerResourceId, when provided, must not be Guid.Empty.");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.StartAnchor!).SetValidator(new AnchorSpecValidator()).When(x => x.StartAnchor is not null);
        RuleFor(x => x.EndAnchor!).SetValidator(new AnchorSpecValidator()).When(x => x.EndAnchor is not null);
    }
}

public sealed class EditCommandValidator : AbstractValidator<EditCommand>
{
    public EditCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.PeriodStart).LessThanOrEqualTo(x => x.PeriodEnd)
            .WithMessage("PeriodStart must be on or before PeriodEnd.");
        RuleFor(x => x.AllocationPercent)
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"AllocationPercent must be in the range (0, {Allocation.MaxAllocationPercent}].");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class SplitAtCommandValidator : AbstractValidator<SplitAtCommand>
{
    public SplitAtCommandValidator() => RuleFor(x => x.Id).NotEqual(Guid.Empty);
}

public sealed class ChangeRateFromCommandValidator : AbstractValidator<ChangeRateFromCommand>
{
    public ChangeRateFromCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.NewRate)
            .GreaterThan(0m).LessThanOrEqualTo(Allocation.MaxAllocationPercent)
            .WithMessage($"NewRate must be in the range (0, {Allocation.MaxAllocationPercent}].");
    }
}

public sealed class MoveCommandValidator : AbstractValidator<MoveCommand>
{
    public MoveCommandValidator() => RuleFor(x => x.Id).NotEqual(Guid.Empty);
}

public sealed class RetargetCommandValidator : AbstractValidator<RetargetCommand>
{
    public RetargetCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.DemandId).NotEqual(Guid.Empty);
    }
}

public sealed class ResizeCommandValidator : AbstractValidator<ResizeCommand>
{
    public ResizeCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x)
            .Must(x => x.NewPeriodStart is not null || x.NewPeriodEnd is not null)
            .WithMessage("Resize requires at least one of NewPeriodStart or NewPeriodEnd.");
        RuleFor(x => x.NewPeriodEnd)
            .GreaterThanOrEqualTo(x => x.NewPeriodStart!.Value)
            .When(x => x.NewPeriodStart is not null && x.NewPeriodEnd is not null)
            .WithMessage("NewPeriodStart must be on or before NewPeriodEnd.");
    }
}

public sealed class ShiftFromCommandValidator : AbstractValidator<ShiftFromCommand>
{
    public ShiftFromCommandValidator()
    {
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
    }
}

public sealed class ReassignCommandValidator : AbstractValidator<ReassignCommand>
{
    public ReassignCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
    }
}

public sealed class ChangeStatusCommandValidator : AbstractValidator<ChangeStatusCommand>
{
    public ChangeStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class DeleteCommandValidator : AbstractValidator<DeleteCommand>
{
    public DeleteCommandValidator() => RuleFor(x => x.Id).NotEqual(Guid.Empty);
}

// ── Demand (Phase 5.0) ──────────────────────────────────────────────────────

public sealed class CreateDemandCommandValidator : AbstractValidator<CreateDemandCommand>
{
    public CreateDemandCommandValidator()
    {
        RuleFor(x => x.ProjectNodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.RoleId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequiredHours!.Value)
            .GreaterThan(TimeSpan.Zero)
            .When(x => x.RequiredHours is not null)
            .WithMessage("RequiredHours, when provided, must be greater than zero.");
        RuleFor(x => x.OwnerResourceId)
            .Must(o => o is null || o != Guid.Empty)
            .WithMessage("OwnerResourceId, when provided, must not be Guid.Empty.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class EditDemandCommandValidator : AbstractValidator<EditDemandCommand>
{
    public EditDemandCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.RoleId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.RoleId is not null)
            .WithMessage("RoleId, when provided, must not be Guid.Empty.");
        RuleFor(x => x.RequiredHours!.Value)
            .GreaterThan(TimeSpan.Zero)
            .When(x => x.RequiredHoursSet && x.RequiredHours is not null)
            .WithMessage("RequiredHours, when provided, must be greater than zero.");
        RuleFor(x => x.OwnerResourceId)
            .Must(o => o is null || o != Guid.Empty)
            .When(x => x.OwnerResourceIdSet)
            .WithMessage("OwnerResourceId, when provided, must not be Guid.Empty.");
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.NotesSet);
    }
}

public sealed class DeleteDemandCommandValidator : AbstractValidator<DeleteDemandCommand>
{
    public DeleteDemandCommandValidator() => RuleFor(x => x.Id).NotEqual(Guid.Empty);
}

// ── Boundaries (ADR-0034) ─────────────────────────────────────────────────

// Shape per kind: the referent the kind takes must be present, and nothing
// else. Scope (I10) and the referent's date (I9) are the service's business.
public sealed class AnchorSpecValidator : AbstractValidator<AnchorSpec>
{
    public AnchorSpecValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.NodeId)
            .NotNull().NotEqual(Guid.Empty)
            .When(x => x.Kind is AnchorKind.NodeStart or AnchorKind.NodeEnd)
            .WithMessage("A NodeStart/NodeEnd anchor requires NodeId.");
        RuleFor(x => x.NodeId)
            .Null()
            .When(x => x.Kind is not (AnchorKind.NodeStart or AnchorKind.NodeEnd))
            .WithMessage("NodeId is only valid on a NodeStart/NodeEnd anchor.");
        RuleFor(x => x.ConstraintId)
            .NotNull().NotEqual(Guid.Empty)
            .When(x => x.Kind == AnchorKind.External)
            .WithMessage("An External anchor requires ConstraintId.");
        RuleFor(x => x.ConstraintId)
            .Null()
            .When(x => x.Kind != AnchorKind.External)
            .WithMessage("ConstraintId is only valid on an External anchor.");
    }
}

public sealed class SetAnchorCommandValidator : AbstractValidator<SetAnchorCommand>
{
    public SetAnchorCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Edge).IsInEnum();
        RuleFor(x => x.Anchor).NotNull().SetValidator(new AnchorSpecValidator());
        RuleFor(x => x.Anchor.Kind)
            .NotEqual(AnchorKind.Pinned)
            .When(x => x.Anchor is not null)
            .WithMessage("setAnchor requires an anchored kind; use pin to release a boundary.");
    }
}

public sealed class PinCommandValidator : AbstractValidator<PinCommand>
{
    public PinCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.Edge).IsInEnum();
    }
}

public sealed class ReplanNodeCommandValidator : AbstractValidator<ReplanNodeCommand>
{
    public ReplanNodeCommandValidator()
    {
        RuleFor(x => x.NodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.PlannedStart!.Value).LessThanOrEqualTo(x => x.PlannedEnd!.Value)
            .When(x => x.PlannedStart.HasValue && x.PlannedEnd.HasValue)
            .WithMessage("PlannedStart must be on or before PlannedEnd.");
    }
}

public sealed class MoveSubtreeCommandValidator : AbstractValidator<MoveSubtreeCommand>
{
    public MoveSubtreeCommandValidator()
    {
        RuleFor(x => x.NodeId).NotEqual(Guid.Empty);
        RuleFor(x => x.DeltaDays).NotEqual(0).WithMessage("DeltaDays must not be zero.");
    }
}

public sealed class SetAvailabilityCommandValidator : AbstractValidator<SetAvailabilityCommand>
{
    public SetAvailabilityCommandValidator()
    {
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.AvailableFrom!.Value).LessThanOrEqualTo(x => x.AvailableUntil!.Value)
            .When(x => x.AvailableFrom.HasValue && x.AvailableUntil.HasValue)
            .WithMessage("AvailableFrom must be on or before AvailableUntil.");
    }
}

public sealed class MoveConstraintCommandValidator : AbstractValidator<MoveConstraintCommand>
{
    public MoveConstraintCommandValidator() => RuleFor(x => x.ConstraintId).NotEqual(Guid.Empty);
}
