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

public sealed class CreateResourceDtoValidator : AbstractValidator<CreateResourceDto>
{
    public CreateResourceDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Windows).SetValidator(new WorkWindowDtoValidator());
        RuleForEach(x => x.Adjustments).SetValidator(new IndividualAdjustmentDtoValidator());
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
