using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// The terms of a card or loan: what it costs, what it is capped at, when it is due.
///
/// Deliberately NOT on <see cref="Account"/>. An account is a place postings land, and its
/// balance is always derived from them — putting a rate or a limit there would mix a fact that
/// changes by agreement in among facts that change by transaction. The balance is never here for
/// the same reason: it is the ledger's answer, not a field.
/// </summary>
public sealed class DebtTerms
{
    public Guid Id { get; private set; }

    /// <summary>
    /// The debt account this describes. One set of terms per account, and the only link this
    /// needs: whose debt it is, is whoever owns the ledger the account sits in.
    /// </summary>
    public AccountId AccountId { get; private set; }

    /// <summary>Annual percentage rate as a percentage — 24.99 means 24.99%, not 0.2499.</summary>
    public decimal AnnualPercentageRate { get; private set; }

    /// <summary>
    /// Null for a loan, which is drawn once rather than revolving.
    ///
    /// A bare decimal, not Money: the account this describes lives in a ledger that has exactly
    /// one currency, so a currency here could only ever agree with it or be wrong, and nothing
    /// would be checking which.
    /// </summary>
    public decimal? CreditLimit { get; private set; }

    public int? StatementDayOfMonth { get; private set; }
    public int? PaymentDueDayOfMonth { get; private set; }
    public decimal? MinimumPayment { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private DebtTerms() { }

    /// <summary>
    /// Takes the account rather than its id, so the pairing is checked here instead of being a
    /// foreign key anybody could point at a cash or expense account. A rate on something that is
    /// not borrowed is not wrong-looking data — it is meaningless data.
    /// </summary>
    public static DebtTerms For(
        Account account,
        decimal annualPercentageRate,
        decimal? creditLimit = null,
        int? statementDayOfMonth = null,
        int? paymentDueDayOfMonth = null,
        decimal? minimumPayment = null)
    {
        if (account.AccountType != AccountType.Liability || !ChartCodes.IsDeclaredDebt(account.Code))
            throw new InvalidOperationException(
                $"{account.Name} is not a borrowing account, so it has no rate or limit.");

        Validate(annualPercentageRate, creditLimit, statementDayOfMonth, paymentDueDayOfMonth, minimumPayment);

        var now = DateTime.UtcNow;
        return new DebtTerms
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            AnnualPercentageRate = annualPercentageRate,
            CreditLimit = creditLimit,
            StatementDayOfMonth = statementDayOfMonth,
            PaymentDueDayOfMonth = paymentDueDayOfMonth,
            MinimumPayment = minimumPayment,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Amend(
        decimal annualPercentageRate,
        decimal? creditLimit,
        int? statementDayOfMonth,
        int? paymentDueDayOfMonth,
        decimal? minimumPayment)
    {
        Validate(annualPercentageRate, creditLimit, statementDayOfMonth, paymentDueDayOfMonth, minimumPayment);

        AnnualPercentageRate = annualPercentageRate;
        CreditLimit = creditLimit;
        StatementDayOfMonth = statementDayOfMonth;
        PaymentDueDayOfMonth = paymentDueDayOfMonth;
        MinimumPayment = minimumPayment;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// One month's rate. A card really charges a daily periodic rate against the average daily
    /// balance; this is the monthly approximation, which is why an accrual posted from it is an
    /// estimate until a statement says otherwise.
    /// </summary>
    public decimal MonthlyRate => AnnualPercentageRate / 100m / 12m;

    /// <summary>How much of the limit is left, given what the ledger says is owed. Null when the
    /// debt does not revolve.</summary>
    public decimal? HeadroomAgainst(decimal balanceOwed) =>
        CreditLimit is null ? null : CreditLimit.Value - balanceOwed;

    private static void Validate(
        decimal apr, decimal? creditLimit, int? statementDay, int? dueDay, decimal? minimumPayment)
    {
        if (apr < 0m)
            throw new ArgumentOutOfRangeException(nameof(apr), "A rate cannot be negative.");
        // A percentage, not a fraction. 0.2499 entered for a 24.99% card is the likely slip, and
        // it would under-accrue by a hundredfold in silence — so only refuse the impossible end.
        if (apr > 100m)
            throw new ArgumentOutOfRangeException(nameof(apr), "A rate above 100% is not a rate anyone was offered.");

        if (creditLimit <= 0m)
            throw new ArgumentOutOfRangeException(nameof(creditLimit), "A credit limit must be positive.");

        if (minimumPayment < 0m)
            throw new ArgumentOutOfRangeException(nameof(minimumPayment), "A minimum payment cannot be negative.");

        // The 29th, 30th and 31st are allowed: real statements land on them, and the caller
        // clamps to the month's length rather than the domain refusing a legitimate day.
        if (statementDay is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(statementDay), "A statement day is a day of the month.");
        if (dueDay is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(dueDay), "A due day is a day of the month.");
    }
}
