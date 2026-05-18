using FluentValidation;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Services.Shared;

namespace ResourcePulse.Services.Resources;

public sealed class IndividualAdjustmentDtoValidator : AbstractValidator<IndividualAdjustmentDto>
{
    public IndividualAdjustmentDtoValidator()
    {
        RuleFor(x => x.DateFrom).LessThanOrEqualTo(x => x.DateTo)
            .WithMessage("DateFrom must be on or before DateTo.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);

        When(x => x.Type == AdjustmentType.ExtraTime, () =>
        {
            RuleFor(x => x.Hours).NotNull()
                .WithMessage("Hours is required for ExtraTime adjustments.");
            RuleFor(x => x.Hours!.Value).GreaterThan(TimeSpan.Zero)
                .When(x => x.Hours is not null)
                .WithMessage("Hours must be positive for ExtraTime adjustments.");
        });
        When(x => x.Type == AdjustmentType.Absence && x.Hours is not null, () =>
        {
            RuleFor(x => x.Hours!.Value).GreaterThan(TimeSpan.Zero)
                .WithMessage("Hours must be positive when provided on an Absence.");
        });
    }
}

public sealed class ResourceSkillDtoValidator : AbstractValidator<ResourceSkillDto>
{
    public ResourceSkillDtoValidator()
    {
        RuleFor(x => x.SkillId).NotEqual(Guid.Empty);
        RuleFor(x => x.Level).IsInEnum();
    }
}

public sealed class ResourceTagDtoValidator : AbstractValidator<ResourceTagDto>
{
    public ResourceTagDtoValidator()
    {
        RuleFor(x => x.TagId).NotEqual(Guid.Empty);
    }
}

public sealed class CreateResourceDtoValidator : AbstractValidator<CreateResourceDto>
{
    public CreateResourceDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Windows).SetValidator(new WorkWindowDtoValidator());
        RuleForEach(x => x.Adjustments).SetValidator(new IndividualAdjustmentDtoValidator());
        RuleForEach(x => x.Skills).SetValidator(new ResourceSkillDtoValidator());
        RuleForEach(x => x.Tags).SetValidator(new ResourceTagDtoValidator());
    }
}

public sealed class UpdateResourceDtoValidator : AbstractValidator<UpdateResourceDto>
{
    public UpdateResourceDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessCalendarId).NotEqual(Guid.Empty)
            .WithMessage("BusinessCalendarId is required.");
    }
}

public sealed class AddOrUpdateResourceSkillDtoValidator : AbstractValidator<AddOrUpdateResourceSkillDto>
{
    public AddOrUpdateResourceSkillDtoValidator()
    {
        RuleFor(x => x.SkillId).NotEqual(Guid.Empty);
        RuleFor(x => x.Level).IsInEnum();
    }
}

public sealed class AddResourceTagDtoValidator : AbstractValidator<AddResourceTagDto>
{
    public AddResourceTagDtoValidator()
    {
        RuleFor(x => x.TagId).NotEqual(Guid.Empty);
    }
}
