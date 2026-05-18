using FluentValidation;

namespace ResourcePulse.Services.Skills;

public sealed class CreateSkillDtoValidator : AbstractValidator<CreateSkillDto>
{
    public CreateSkillDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}

public sealed class UpdateSkillDtoValidator : AbstractValidator<UpdateSkillDto>
{
    public UpdateSkillDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
    }
}
