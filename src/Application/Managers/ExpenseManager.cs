using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Ports;
using Finance.Application.Repositories;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
namespace Finance.Application.Managers;

internal sealed class ExpenseManager : IExpenseManager
{
    private readonly IExpenseRepository _repository;
    private readonly IExpensePaymentRepository _paymentRepository;
    private readonly IExpenseSplitRepository _splitRepository;
    private readonly IExpenseSplitPaymentRepository _splitPaymentRepository;
    private readonly IHouseholdMembershipRepository _membershipRepository;
    private readonly IFinancialConnectionRepository _connectionRepository;

    public ExpenseManager(
        IExpenseRepository repository,
        IExpensePaymentRepository paymentRepository,
        IExpenseSplitRepository splitRepository,
        IExpenseSplitPaymentRepository splitPaymentRepository,
        IHouseholdMembershipRepository membershipRepository,
        IFinancialConnectionRepository connectionRepository)
    {
        _repository = repository;
        _paymentRepository = paymentRepository;
        _splitRepository = splitRepository;
        _splitPaymentRepository = splitPaymentRepository;
        _membershipRepository = membershipRepository;
        _connectionRepository = connectionRepository;
    }

    // ── Personal expense operations ───────────────────────────────────────────

    public async Task<ExpenseDto> CreateAsync(CreateExpenseCommand request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ExpenseCategory>(request.Category, ignoreCase: true, out var category))
            category = ExpenseCategory.Other;

        var expense = Expense.Create(
            UserId.Create(request.UserId),
            request.Title,
            Money.Create(request.Amount, request.Currency),
            category,
            request.DueDate,
            ParseSchedule(request.RecurrenceFrequency, request.RecurrenceStartDate ?? request.DueDate, request.RecurrenceEndDate),
            request.Description);

        await _repository.AddAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ExpenseDto?> UpdateAsync(UpdateExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        if (!Enum.TryParse<ExpenseCategory>(request.Category, ignoreCase: true, out var category))
            category = ExpenseCategory.Other;

        expense.Update(
            request.Title,
            Money.Create(request.Amount, request.Currency),
            category,
            request.DueDate,
            ParseSchedule(request.RecurrenceFrequency, request.RecurrenceStartDate ?? request.DueDate, request.RecurrenceEndDate),
            request.Description);

        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ExpenseDto?> DeleteAsync(DeleteExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        if (expense.TryDeactivate())
            await _repository.UpdateAsync(expense, cancellationToken);

        var suggestion = await _connectionRepository.GetSuggestionByLinkedEntityIdAsync(request.ExpenseId, cancellationToken);
        if (suggestion is not null)
        {
            suggestion.Unlink();
            await _connectionRepository.SaveSuggestionAsync(suggestion, cancellationToken);
        }

        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    // ── Household expense operations ──────────────────────────────────────────

    public async Task<HouseholdExpenseDto> CreateHouseholdExpenseAsync(CreateHouseholdExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = Expense.CreateHousehold(
            HouseholdId.Create(request.HouseholdId),
            UserId.Create(request.CreatedBy),
            request.Title,
            Money.Create(request.Amount, request.Currency),
            request.Category,
            request.DueDate,
            BuildRecurrence(request.RecurrenceFrequency, request.RecurrenceStartDate, request.RecurrenceEndDate),
            request.Description);

        await _repository.AddAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToHouseholdResponse(expense);
    }

    public async Task<HouseholdExpenseDto?> UpdateHouseholdExpenseAsync(UpdateHouseholdExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        if (expense.HouseholdId.HasValue)
        {
            var membership = await _membershipRepository.GetByUserAndHouseholdAsync(
                UserId.Create(request.CallerId), expense.HouseholdId.Value, cancellationToken);
            if (membership is null)
                throw new UnauthorizedAccessException("Caller is not a member of this household.");
        }

        expense.Update(
            request.Title,
            Money.Create(request.Amount, request.Currency),
            request.Category,
            request.DueDate,
            BuildRecurrence(request.RecurrenceFrequency, request.RecurrenceStartDate, request.RecurrenceEndDate),
            request.Description);

        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToHouseholdResponse(expense);
    }

    public async Task<HouseholdExpenseDto?> DeactivateHouseholdExpenseAsync(DeactivateExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        if (expense.HouseholdId.HasValue)
        {
            var membership = await _membershipRepository.GetByUserAndHouseholdAsync(
                UserId.Create(request.CallerId), expense.HouseholdId.Value, cancellationToken);
            if (membership is null)
                throw new UnauthorizedAccessException("Caller is not a member of this household.");
        }

        expense.Deactivate();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToHouseholdResponse(expense);
    }

    // ── Split management ──────────────────────────────────────────────────────

    public async Task<SplitDto> UpsertSplitAsync(UpsertSplitCommand request, CancellationToken cancellationToken = default)
    {
        var money = Money.Create(request.Amount, request.Currency);
        var expenseId = ExpenseId.Create(request.ExpenseId);

        if (request.SplitId.HasValue)
        {
            var existing = await _splitRepository.GetByIdAsync(ExpenseSplitId.Create(request.SplitId.Value), cancellationToken);
            if (existing is not null)
            {
                existing.Update(money);
                await _splitRepository.UpdateAsync(existing, cancellationToken);
                await _splitRepository.CommitAsync(cancellationToken);
                return ExpenseMapper.ToSplitResponse(existing);
            }
        }

        var duplicate = await _splitRepository.GetByExpenseAndMembershipAsync(
            expenseId,
            MembershipId.Create(request.MembershipId),
            cancellationToken);

        if (duplicate is not null)
            throw new InvalidOperationException("A split for this member already exists on this expense.");

        var split = ExpenseSplit.Create(
            expenseId,
            HouseholdId.Create(request.HouseholdId),
            MembershipId.Create(request.MembershipId),
            UserId.Create(request.UserId),
            money);

        await _splitRepository.AddAsync(split, cancellationToken);
        await _splitRepository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToSplitResponse(split);
    }

    public async Task<SplitDto?> RemoveSplitAsync(RemoveSplitCommand request, CancellationToken cancellationToken = default)
    {
        var split = await _splitRepository.GetByIdAsync(ExpenseSplitId.Create(request.SplitId), cancellationToken);
        if (split is null) return null;

        var membership = await _membershipRepository.GetByUserAndHouseholdAsync(
            UserId.Create(request.CallerId), split.HouseholdId, cancellationToken);
        if (membership is null || membership.Role == HouseholdRole.Member)
            throw new UnauthorizedAccessException("Only household Admins and Owners can remove splits.");

        split.Remove();
        await _splitRepository.RemoveAsync(split, cancellationToken);
        await _splitRepository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToSplitResponse(split);
    }

    public async Task SplitEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(expenseId), cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");

        if (expense.HouseholdId is null)
            throw new InvalidOperationException("Cannot split a personal expense.");

        if (membershipIds.Count == 0)
            throw new ArgumentException("At least one membership is required.", nameof(membershipIds));

        var perMember = expense.Amount.Amount / membershipIds.Count;
        var money = Money.Create(perMember, expense.Amount.Currency);

        var memberships = await _membershipRepository.GetByIdsAsync(
            membershipIds.Select(MembershipId.Create).ToList(), cancellationToken);

        foreach (var membership in memberships)
        {
            var existing = await _splitRepository.GetByExpenseAndMembershipAsync(expense.Id, membership.Id, cancellationToken);
            if (existing is not null)
            {
                existing.Update(money);
                await _splitRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                var split = ExpenseSplit.Create(expense.Id, expense.HouseholdId.Value, membership.Id, membership.UserId, money);
                await _splitRepository.AddAsync(split, cancellationToken);
            }
        }
        await _splitRepository.CommitAsync(cancellationToken);
    }

    // ── Unified payment ───────────────────────────────────────────────────────

    public async Task MarkPaidAsync(MarkExpensePaidCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");

        var occurrenceDate = DateTime.SpecifyKind(request.OccurrenceDate.Date, DateTimeKind.Utc);

        if (expense.HouseholdId.HasValue)
        {
            // Household expense: record split payment for the caller's split
            var userId = UserId.Create(request.UserId);
            var split = await _splitRepository.GetByExpenseAndUserAsync(expense.Id, userId, cancellationToken)
                ?? throw new InvalidOperationException("No split found for this user on this expense.");

            var existing = await _splitPaymentRepository.GetAsync(split.Id, occurrenceDate, cancellationToken);
            if (existing is not null) return; // idempotent

            var payment = ExpenseSplitPayment.Create(split.Id, expense.Id, expense.HouseholdId.Value, userId, occurrenceDate, request.TransactionReference);
            await _splitPaymentRepository.AddAsync(payment, cancellationToken);
        }
        else
        {
            // Personal expense: record direct payment
            if (expense.UserId.Value != request.UserId)
                throw new InvalidOperationException("Access denied.");

            var existing = await _paymentRepository.GetAsync(expense.Id, occurrenceDate, cancellationToken);
            if (existing is not null) return; // idempotent

            var payment = ExpensePayment.Create(expense.Id, expense.UserId, occurrenceDate, request.TransactionReference);
            await _paymentRepository.AddAsync(payment, cancellationToken);
        }

        await _repository.CommitAsync(cancellationToken);
    }

    public async Task MarkUnpaidAsync(MarkExpenseUnpaidCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return;

        var occurrenceDate = DateTime.SpecifyKind(request.OccurrenceDate.Date, DateTimeKind.Utc);

        if (expense.HouseholdId.HasValue)
        {
            var userId = UserId.Create(request.UserId);
            var split = await _splitRepository.GetByExpenseAndUserAsync(expense.Id, userId, cancellationToken);
            if (split is null) return;

            var payment = await _splitPaymentRepository.GetAsync(split.Id, occurrenceDate, cancellationToken);
            if (payment is null) return;

            await _splitPaymentRepository.RemoveAsync(payment, cancellationToken);
        }
        else
        {
            var payment = await _paymentRepository.GetAsync(ExpenseId.Create(request.ExpenseId), occurrenceDate, cancellationToken);
            if (payment is null) return;

            await _paymentRepository.RemoveAsync(payment, cancellationToken);
        }

        await _repository.CommitAsync(cancellationToken);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RecurrenceSchedule? ParseSchedule(string? frequency, DateTime? startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(frequency)
            || !Enum.TryParse<RecurrenceFrequency>(frequency, ignoreCase: true, out var freq))
            return null;

        return RecurrenceSchedule.Create(freq, startDate ?? DateTime.UtcNow, endDate);
    }

    private static RecurrenceSchedule? BuildRecurrence(RecurrenceFrequency? frequency, DateTime? startDate, DateTime? endDate)
    {
        if (!frequency.HasValue) return null;
        return RecurrenceSchedule.Create(frequency.Value, startDate ?? DateTime.UtcNow.Date, endDate);
    }
}
