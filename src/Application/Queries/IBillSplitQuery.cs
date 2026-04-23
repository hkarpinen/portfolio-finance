using Bills.Application.Contracts;
using Bills.Domain.ValueObjects;

namespace Bills.Application.Queries;

public interface IBillSplitQuery
{
    Task<IReadOnlyCollection<SplitWithBillDetail>> ListByUserWithBillDetailsAsync(
        UserId userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
