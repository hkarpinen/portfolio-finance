using System.Reflection;
using Client.Filters;
using Client.Validators;
using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Tests;

/// <summary>
/// Every body-bound command has a validator, and this is the proof that each one bites.
///
/// The gap they close is narrow but real. String length was checked in exactly one place — the
/// column — so an over-long title travelled the whole way down and failed as a storage error,
/// surfacing as a 500 on what is plainly a bad request. And an enum arriving as a number outside
/// its range binds without complaint in System.Text.Json, so a category nobody named reached the
/// aggregate looking like a category.
///
/// What is deliberately NOT here: rules the domain owns. A percentage over 100, settling up with
/// yourself, a tax type that is engine-computed — those are answered by the aggregate, which the
/// pipeline already turns into a 400. Restating them here would put one rule in two places.
/// </summary>
public class RequestValidatorTests
{
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private static CreateRecurringExpenseCommand Rent() => new(
        GroupId: Guid.NewGuid(), CallerUserId: Guid.NewGuid(), Title: "Rent", Amount: 1000m,
        Currency: "USD", Category: ExpenseCategory.Rent,
        Frequency: RecurrenceFrequency.Monthly, AnchorDate: Jan3);

    [Fact]
    public void AValidScheduleIsAccepted()
    {
        Assert.True(new CreateRecurringExpenseRequestValidator().Validate(Rent()).IsValid);
    }

    // 200 is the column. Past it the insert fails, which is a 500 for what is a bad request.
    [Fact]
    public void ATitleLongerThanItsColumnIsRejected()
    {
        var tooLong = Rent() with { Title = new string('x', 201) };

        Assert.False(new CreateRecurringExpenseRequestValidator().Validate(tooLong).IsValid);
    }

    [Fact]
    public void AnEmptyTitleOrANonPositiveAmountIsRejected()
    {
        var v = new CreateRecurringExpenseRequestValidator();

        Assert.False(v.Validate(Rent() with { Title = "   " }).IsValid);
        Assert.False(v.Validate(Rent() with { Amount = 0m }).IsValid);
        Assert.False(v.Validate(Rent() with { Amount = -50m }).IsValid);
    }

    // Nothing rejects this at the binding layer: an int outside the enum's range becomes a value
    // of that enum type that no member names, and every switch over it falls to its default.
    [Fact]
    public void ACategoryOutsideTheEnumIsRejected()
    {
        var bogus = Rent() with { Category = (ExpenseCategory)9999 };

        Assert.False(new CreateRecurringExpenseRequestValidator().Validate(bogus).IsValid);
    }

    [Fact]
    public void AScheduleEndingBeforeItStartsIsRejected()
    {
        var backwards = Rent() with { EndDate = Jan3.AddDays(-1) };

        Assert.False(new CreateRecurringExpenseRequestValidator().Validate(backwards).IsValid);
    }

    [Fact]
    public void AnAmendmentIsHeldToTheSameLengths()
    {
        var v = new AmendRecurringExpenseRequestValidator();
        var amend = new AmendRecurringExpenseCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Rent", 1100m, "USD", ExpenseCategory.Rent);

        Assert.True(v.Validate(amend).IsValid);
        Assert.False(v.Validate(amend with { Title = new string('x', 201) }).IsValid);
        Assert.False(v.Validate(amend with { Currency = "DOLLARS" }).IsValid);
    }

    [Fact]
    public void ADeductionLabelLongerThanItsColumnIsRejected()
    {
        var v = new AddDeductionRequestValidator();
        var good = new AddDeductionCommand(Guid.NewGuid(), new PayrollDeductionDto(
            DeductionType.HealthInsurance, "Health", DeductionCalculationMethod.FixedAmount,
            120m, IsEmployerSponsored: false));

        Assert.True(v.Validate(good).IsValid);
        Assert.False(v.Validate(good with
        {
            Deduction = good.Deduction with { Label = new string('x', 201) }
        }).IsValid);
    }

    [Fact]
    public void ADeductionTypeThatNamesNothingIsRejected()
    {
        var v = new RemoveDeductionRequestValidator();
        var command = new RemoveDeductionCommand(Guid.NewGuid(), "HealthInsurance", "Health");

        Assert.True(v.Validate(command).IsValid);
        Assert.False(v.Validate(command with { DeductionType = "NotAThing" }).IsValid);
    }

    // Null is the documented way to clear a profile, so it has to survive the validator.
    [Fact]
    public void ClearingATaxProfileIsAllowedButABadOneIsNot()
    {
        var v = new SetTaxProfileRequestValidator();
        var incomeId = Guid.NewGuid();

        Assert.True(v.Validate(new SetTaxProfileCommand(incomeId, null)).IsValid);
        Assert.True(v.Validate(new SetTaxProfileCommand(incomeId, new TaxProfileDto(
            FilingStatus.Single, "CA", 1, 1))).IsValid);
        Assert.False(v.Validate(new SetTaxProfileCommand(incomeId, new TaxProfileDto(
            FilingStatus.Single, "California", 1, 1))).IsValid);
        Assert.False(v.Validate(new SetTaxProfileCommand(incomeId, new TaxProfileDto(
            FilingStatus.Single, "CA", -1, 1))).IsValid);
    }

    [Fact]
    public void SplittingAmongNobodyOrAmongTheSamePersonTwiceIsRejected()
    {
        var v = new SplitEvenlyBodyValidator();
        var hank = Guid.NewGuid();

        Assert.True(v.Validate(new SplitEvenlyBody([hank, Guid.NewGuid()])).IsValid);
        Assert.False(v.Validate(new SplitEvenlyBody([])).IsValid);
        Assert.False(v.Validate(new SplitEvenlyBody([hank, hank])).IsValid);
    }

    [Fact]
    public void ASettleUpWithNoAmountOrNoRecipientIsRejected()
    {
        var v = new SettleUpTransferBodyValidator();
        var good = new SettleUpTransferBody(Guid.NewGuid(), 30m, "USD");

        Assert.True(v.Validate(good).IsValid);
        Assert.False(v.Validate(good with { Amount = 0m }).IsValid);
        Assert.False(v.Validate(good with { ToUserId = Guid.Empty }).IsValid);
    }

    // Binds to default(DateTime) when the field is absent — a real date the schedule never named.
    [Fact]
    public void AnOccurrenceWithNoDateIsRejected()
    {
        var v = new PaymentOccurrenceBodyValidator();

        Assert.True(v.Validate(new PaymentOccurrenceBody(Jan3)).IsValid);
        Assert.False(v.Validate(new PaymentOccurrenceBody(default)).IsValid);
    }

    [Fact]
    public void ALinkWithNoTokenIsRejected()
    {
        var v = new LinkConnectionRequestValidator();

        Assert.True(v.Validate(new LinkConnectionCommand("public-sandbox-abc", "ins_1", "Chase")).IsValid);
        Assert.False(v.Validate(new LinkConnectionCommand("", "ins_1", "Chase")).IsValid);
        Assert.False(v.Validate(new LinkConnectionCommand(
            "public-sandbox-abc", "ins_1", new string('x', 201))).IsValid);
    }
}

/// <summary>
/// The list above is today's answer. This is what keeps it true.
///
/// A validator is registered by assembly scan, so a command with none is not an error anywhere —
/// it simply is not validated, and nothing says so. Adding a `[FromBody]` parameter without writing
/// a validator for its type fails here instead of shipping an unchecked door.
/// </summary>
public class EveryBoundBodyHasAValidatorTests
{
    private static readonly Assembly ClientAssembly = typeof(RequireGroupMembershipAttribute).Assembly;

    private static IEnumerable<Type> BoundBodyTypes =>
        ClientAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters())
            .Where(p => p.GetCustomAttribute<FromBodyAttribute>() is not null)
            .Select(p => p.ParameterType)
            .Distinct();

    private static IEnumerable<Type> ValidatedTypes =>
        ClientAssembly.GetTypes()
            .Where(t => !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct();

    [Fact]
    public void EveryTypeBoundFromABodyHasOne()
    {
        var validated = ValidatedTypes.ToHashSet();
        var unguarded = BoundBodyTypes.Where(t => !validated.Contains(t)).Select(t => t.Name).ToList();

        Assert.True(unguarded.Count == 0,
            "These types are bound from a request body with no validator, so nothing checks their "
            + "lengths, ranges or required fields before they reach a manager: "
            + string.Join(", ", unguarded));
    }

    // Guards the guard: if the reflection above stops finding parameters, the test above passes
    // vacuously and the whole check quietly stops meaning anything. Sixteen today — the floor is
    // deliberately lower so retiring an endpoint does not fail this, only breaking the sweep does.
    [Fact]
    public void TheSweepActuallyFindsBoundBodies()
    {
        Assert.True(BoundBodyTypes.Count() >= 10);
    }
}
