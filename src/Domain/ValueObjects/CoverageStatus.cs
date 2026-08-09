namespace Finance.Domain.ValueObjects;

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
