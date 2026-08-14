using Finance.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Plaid;

/// <summary>
/// Frees a bank suggestion when the thing it created is deleted, so it can be offered again.
///
/// ExpenseManager used to do this itself, holding a Plaid repository to reach a suggestion — which
/// is how another company's data ended up in the middle of an expense use case. Deleting an expense
/// already announces itself; this listens, and the bank side keeps its own concerns.
/// </summary>
internal sealed class SuggestionUnlinkConsumer : IConsumer<ExpenseDeactivated>
{
    private readonly IFinancialConnectionRepository _repo;
    private readonly ILogger<SuggestionUnlinkConsumer> _logger;

    public SuggestionUnlinkConsumer(
        IFinancialConnectionRepository repo, ILogger<SuggestionUnlinkConsumer> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExpenseDeactivated> context)
    {
        var expenseId = context.Message.ExpenseId.Value;
        var ct = context.CancellationToken;

        var suggestion = await _repo.GetSuggestionByLinkedEntityIdAsync(expenseId, ct);
        if (suggestion is null) return;

        suggestion.Unlink();
        await _repo.SaveSuggestionAsync(suggestion, ct);
        await _repo.CommitAsync(ct);

        _logger.LogInformation(
            "Unlinked suggestion {SuggestionId} — expense {ExpenseId} was deleted.",
            suggestion.Id, expenseId);
    }
}
