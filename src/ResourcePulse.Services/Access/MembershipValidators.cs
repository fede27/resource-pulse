using FluentValidation;

namespace ResourcePulse.Services.Access;

public sealed class InviteMembershipDtoValidator : AbstractValidator<InviteMembershipDto>
{
    public InviteMembershipDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class UpdateMembershipRoleDtoValidator : AbstractValidator<UpdateMembershipRoleDto>
{
    public UpdateMembershipRoleDtoValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
