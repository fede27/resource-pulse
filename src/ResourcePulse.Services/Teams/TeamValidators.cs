using FluentValidation;

namespace ResourcePulse.Services.Teams;

public sealed class CreateTeamDtoValidator : AbstractValidator<CreateTeamDto>
{
    public CreateTeamDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateTeamDtoValidator : AbstractValidator<UpdateTeamDto>
{
    public UpdateTeamDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
