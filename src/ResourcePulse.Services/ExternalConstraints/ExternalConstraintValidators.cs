using FluentValidation;

namespace ResourcePulse.Services.ExternalConstraints;

public sealed class CreateExternalConstraintDtoValidator : AbstractValidator<CreateExternalConstraintDto>
{
    public CreateExternalConstraintDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Authority).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateExternalConstraintDtoValidator : AbstractValidator<UpdateExternalConstraintDto>
{
    public UpdateExternalConstraintDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Authority).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
