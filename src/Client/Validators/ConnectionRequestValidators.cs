using Finance.Application.Commands;
using FluentValidation;

namespace Client.Validators;

/// <summary>
/// The one-time token Plaid Link hands back, plus what it says the institution was. The token is
/// exchanged and never stored; the two institution fields are, at 100 and 200 characters.
/// </summary>
public sealed class LinkConnectionRequestValidator : AbstractValidator<LinkConnectionCommand>
{
    public LinkConnectionRequestValidator()
    {
        RuleFor(x => x.PublicToken).NotEmpty().MaximumLength(500);
        RuleFor(x => x.InstitutionId).MaximumLength(100);
        RuleFor(x => x.InstitutionName).MaximumLength(200);
    }
}
