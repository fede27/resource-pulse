using FluentValidation;

namespace ResourcePulse.Services.Configuration;

// Boundary (shape) validation only. Cross-field domain rules — bands strictly
// increasing / first at 0, frozen < slushy, non-empty level set — are enforced by
// the domain aggregates (single source) and surfaced as Validation results.

public sealed class UpdateLoadBandConfigurationDtoValidator : AbstractValidator<UpdateLoadBandConfigurationDto>
{
    public UpdateLoadBandConfigurationDtoValidator()
    {
        RuleFor(x => x.Bands).NotEmpty();
        RuleForEach(x => x.Bands).ChildRules(b =>
        {
            b.RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
            b.RuleFor(x => x.LowerBound).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class DurationDtoValidator : AbstractValidator<DurationDto>
{
    public DurationDtoValidator()
    {
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Unit).IsInEnum();
    }
}

public sealed class UpdateTimeFenceConfigurationDtoValidator : AbstractValidator<UpdateTimeFenceConfigurationDto>
{
    public UpdateTimeFenceConfigurationDtoValidator()
    {
        RuleFor(x => x.FrozenHorizon).NotNull().SetValidator(new DurationDtoValidator());
        RuleFor(x => x.SlushyHorizon).NotNull().SetValidator(new DurationDtoValidator());
    }
}

public sealed class UpdateBucketingDefaultsDtoValidator : AbstractValidator<UpdateBucketingDefaultsDto>
{
    public UpdateBucketingDefaultsDtoValidator()
    {
        RuleFor(x => x.PrimaryGrain).IsInEnum();
        RuleFor(x => x.SecondaryGrain).IsInEnum();
    }
}

public sealed class UpdateCommitmentPolicyDtoValidator : AbstractValidator<UpdateCommitmentPolicyDto>
{
    public UpdateCommitmentPolicyDtoValidator()
    {
        RuleFor(x => x.HardCommitLevels).NotEmpty();
        RuleForEach(x => x.HardCommitLevels).IsInEnum();
    }
}
