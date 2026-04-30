using Finance.Application.Contracts;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Mappers;

public static class IncomeMapper
{
    public static IncomeResponse ToResponse(IncomeSource income) => new(
        income.Id.Value,
        income.UserId.Value,
        income.Amount.Amount,
        income.Amount.Currency,
        income.Source,
        income.RecurrenceSchedule.Frequency,
        income.RecurrenceSchedule.StartDate,
        income.RecurrenceSchedule.EndDate,
        income.IsActive,
        income.LastPaymentDate,
        income.CreatedAt,
        income.UpdatedAt,
        ToTaxProfileDto(income.TaxProfile),
        income.Deductions.Select(ToDeductionDto).ToList().AsReadOnly());

    public static TaxProfileDto? ToTaxProfileDto(TaxWithholdingProfile? profile) =>
        profile is null ? null : new TaxProfileDto(
            profile.FilingStatus.ToString(),
            profile.StateCode,
            profile.FederalAllowances,
            profile.StateAllowances);

    public static PayrollDeductionDto ToDeductionDto(PayrollDeduction d) => new(
        d.Type.ToString(),
        d.Label,
        d.Method.ToString(),
        d.Value,
        d.IsEmployerSponsored,
        d.Frequency.ToString(),
        d.IsTaxExempt);
}
