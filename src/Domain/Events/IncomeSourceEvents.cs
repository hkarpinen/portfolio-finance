using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record IncomeSourceCreated(
    IncomeId IncomeId,
    UserId UserId,
    Money Amount,
    string Source,
    RecurrenceSchedule RecurrenceSchedule) : DomainEvent;

public record IncomeSourceUpdated(
    IncomeId IncomeId,
    Money Amount,
    string Source) : DomainEvent;

public record IncomeSourceDeactivated(
    IncomeId IncomeId) : DomainEvent;

public record IncomeSourceTaxProfileSet(
    IncomeId IncomeId,
    TaxWithholdingProfile? TaxProfile) : DomainEvent;

public record IncomeSourceDeductionAdded(
    IncomeId IncomeId,
    PayrollDeduction Deduction) : DomainEvent;

public record IncomeSourceDeductionRemoved(
    IncomeId IncomeId,
    DeductionType DeductionType,
    string Label) : DomainEvent;

public record IncomeSourceDeductionUpdated(
    IncomeId IncomeId,
    PayrollDeduction Deduction) : DomainEvent;
