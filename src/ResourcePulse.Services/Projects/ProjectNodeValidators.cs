using FluentValidation;
using ResourcePulse.Domain.Projects;

namespace ResourcePulse.Services.Projects;

public sealed class CreateProjectNodeDtoValidator : AbstractValidator<CreateProjectNodeDto>
{
    public CreateProjectNodeDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Code).MaximumLength(100);
        RuleFor(x => x.NodeType).IsInEnum();

        // Root nodes don't carry a parent and must specify Type + CommitmentLevel.
        When(x => x.NodeType == ProjectNodeType.Project, () =>
        {
            RuleFor(x => x.Type).NotNull().WithMessage("Type is required for Project root nodes.");
            RuleFor(x => x.CommitmentLevel).NotNull().WithMessage("CommitmentLevel is required for Project root nodes.");
        });

        // Non-root nodes require a parent.
        When(x => x.NodeType != ProjectNodeType.Project, () =>
        {
            RuleFor(x => x.ParentId).NotNull().NotEqual(Guid.Empty)
                .WithMessage("ParentId is required for Phase and WorkPackage nodes.");
        });
    }
}

public sealed class UpdateProjectNodeDtoValidator : AbstractValidator<UpdateProjectNodeDto>
{
    public UpdateProjectNodeDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Code).MaximumLength(100);
    }
}

public sealed class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
{
    public UpdateProjectDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CommitmentLevel).IsInEnum();
    }
}

public sealed class ReparentDtoValidator : AbstractValidator<ReparentDto>
{
    public ReparentDtoValidator()
    {
        RuleFor(x => x.NewParentId).NotEqual(Guid.Empty);
    }
}

public sealed class BaselineDtoValidator : AbstractValidator<BaselineDto>
{
    public BaselineDtoValidator()
    {
        RuleFor(x => x.Start).LessThanOrEqualTo(x => x.End)
            .WithMessage("Start must be on or before End.");
    }
}

public sealed class RebaselineDtoValidator : AbstractValidator<RebaselineDto>
{
    public RebaselineDtoValidator()
    {
        RuleFor(x => x.Start).LessThanOrEqualTo(x => x.End)
            .WithMessage("Start must be on or before End.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ReplanDtoValidator : AbstractValidator<ReplanDto>
{
    public ReplanDtoValidator()
    {
        RuleFor(x => x.Start!.Value).LessThanOrEqualTo(x => x.End!.Value)
            .When(x => x.Start.HasValue && x.End.HasValue)
            .WithMessage("Start must be on or before End.");
    }
}

public sealed class BackfillActualsDtoValidator : AbstractValidator<BackfillActualsDto>
{
    public BackfillActualsDtoValidator()
    {
        RuleFor(x => x.Start!.Value).LessThanOrEqualTo(x => x.End!.Value)
            .When(x => x.Start.HasValue && x.End.HasValue)
            .WithMessage("Start must be on or before End.");
    }
}

public sealed class ReasonDtoValidator : AbstractValidator<ReasonDto>
{
    public ReasonDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class AddOrUpdateProjectSkillRequirementDtoValidator : AbstractValidator<AddOrUpdateProjectSkillRequirementDto>
{
    public AddOrUpdateProjectSkillRequirementDtoValidator()
    {
        RuleFor(x => x.SkillId).NotEqual(Guid.Empty);
        RuleFor(x => x.MinLevel).IsInEnum();
    }
}

public sealed class AddProjectNodeTagDtoValidator : AbstractValidator<AddProjectNodeTagDto>
{
    public AddProjectNodeTagDtoValidator()
    {
        RuleFor(x => x.TagId).NotEqual(Guid.Empty);
    }
}

public sealed class SetPlanningModeDtoValidator : AbstractValidator<SetPlanningModeDto>
{
    public SetPlanningModeDtoValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();
        // FixedWork ⇒ EstimatedWork required and positive; otherwise must be null.
        // Domain re-checks; this catches obvious shape errors at the boundary.
        When(x => x.Mode == PlanningMode.FixedWork, () =>
        {
            RuleFor(x => x.EstimatedWork).NotNull()
                .WithMessage("EstimatedWork is required when Mode is FixedWork.");
            RuleFor(x => x.EstimatedWork!.Value).GreaterThan(TimeSpan.Zero)
                .When(x => x.EstimatedWork.HasValue)
                .WithMessage("EstimatedWork must be greater than zero.");
        });
        When(x => x.Mode != PlanningMode.FixedWork, () =>
        {
            RuleFor(x => x.EstimatedWork).Null()
                .WithMessage("EstimatedWork is only allowed when Mode is FixedWork.");
        });
    }
}

public sealed class UpdateEstimatedWorkDtoValidator : AbstractValidator<UpdateEstimatedWorkDto>
{
    public UpdateEstimatedWorkDtoValidator()
    {
        RuleFor(x => x.EstimatedWork).GreaterThan(TimeSpan.Zero)
            .WithMessage("EstimatedWork must be greater than zero.");
    }
}
