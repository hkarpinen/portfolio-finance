namespace Finance.Domain.ValueObjects;

/// <summary>IRS filing status — used when computing federal and state income tax withholding.</summary>
public enum FilingStatus
{
    Single,
    MarriedFilingJointly,
    MarriedFilingSeparately,
    HeadOfHousehold,
}
