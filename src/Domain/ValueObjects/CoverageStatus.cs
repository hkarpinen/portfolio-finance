namespace Finance.Domain.ValueObjects;

/// <summary>
/// Household income vs. bill coverage assessment produced by
/// <see cref="Finance.Domain.Engines.HouseholdCoverageEngine"/>.
/// </summary>
public sealed record CoverageStatus(
    Guid HouseholdId,
    decimal TotalGrossIncomeAmount,
    decimal TotalNetIncomeAmount,
    decimal TotalBillsAmount,
    decimal Ratio,
    bool IsFullyCovered,
    string Status,
    DateTime PeriodStart,
    DateTime PeriodEnd);
