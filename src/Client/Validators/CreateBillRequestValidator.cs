using Finance.Application.Contracts;
using FluentValidation;

namespace Client.Validators;

public sealed class CreateBillRequestValidator : AbstractValidator<CreateBillRequest>
{
    public CreateBillRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.RecurrenceEndDate)
            .GreaterThanOrEqualTo(x => x.RecurrenceStartDate!.Value)
            .When(x => x.RecurrenceStartDate.HasValue && x.RecurrenceEndDate.HasValue);
    }
}

public sealed class UpdateBillRequestValidator : AbstractValidator<UpdateBillRequest>
{
    public UpdateBillRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DueDate).NotEmpty();
    }
}
