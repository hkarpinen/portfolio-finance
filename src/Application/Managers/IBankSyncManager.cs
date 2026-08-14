using Finance.Domain.Aggregates;

namespace Finance.Application.Managers;

public interface IBankSyncManager
{
    // The caller is responsible for any auth checks before invoking this.
    Task<(int Added, int Modified, int Removed, bool HasMore)> SyncConnectionAsync(
        FinancialConnection connection,
        CancellationToken cancellationToken = default);

    // Also auto-links new suggestions to existing income / expense entities.
    Task RefreshSuggestionsAsync(
        FinancialConnection connection,
        CancellationToken ct = default);
}
