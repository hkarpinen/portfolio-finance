namespace Finance.Domain.ValueObjects;

/// <summary>
/// Group income vs. bill coverage assessment produced by
/// <see cref="Finance.Domain.Engines.GroupCoverageEngine"/>.
/// </summary>
public sealed record CoverageStatus(
    Guid GroupId,
    decimal TotalGrossIncomeAmount,
    decimal TotalNetIncomeAmount,
    decimal TotalBillsAmount,
    decimal Ratio,
    bool IsFullyCovered,
    string Status,
    DateTime PeriodStart,
    DateTime PeriodEnd);
