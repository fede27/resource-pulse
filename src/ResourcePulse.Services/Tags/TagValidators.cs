using FluentValidation;

namespace ResourcePulse.Services.Tags;

public sealed class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
{
    public CreateTagDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateTagDtoValidator : AbstractValidator<UpdateTagDto>
{
    public UpdateTagDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
