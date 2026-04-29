using Finance.Application.Contracts;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers.Dependencies;

public interface IHouseholdRepository
{
    Task AddAsync(Household household, CancellationToken cancellationToken = default);
    Task UpdateAsync(Household household, CancellationToken cancellationToken = default);
    Task<Household?> GetByIdAsync(HouseholdId householdId, CancellationToken cancellationToken = default);
}

public interface IHouseholdMembershipRepository
{
    Task AddAsync(HouseholdMembership membership, CancellationToken cancellationToken = default);
    Task UpdateAsync(HouseholdMembership membership, CancellationToken cancellationToken = default);
    Task<HouseholdMembership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken = default);
    Task<HouseholdMembership?> GetByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<HouseholdMembership>> ListByHouseholdAsync(
        HouseholdId householdId,
        CancellationToken cancellationToken = default);
}

public interface IBillRepository
{
    Task AddAsync(Bill bill, CancellationToken cancellationToken = default);
    Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default);
    Task<Bill?> GetByIdAsync(BillId billId, CancellationToken cancellationToken = default);
}

public interface IBillSplitRepository
{
    Task AddAsync(BillSplit split, CancellationToken cancellationToken = default);
    Task UpdateAsync(BillSplit split, CancellationToken cancellationToken = default);
    Task RemoveAsync(BillSplit split, CancellationToken cancellationToken = default);
    Task<BillSplit?> GetByIdAsync(SplitId splitId, CancellationToken cancellationToken = default);
    Task<BillSplit?> GetByBillAndMembershipAsync(
        BillId billId,
        MembershipId membershipId,
        CancellationToken cancellationToken = default);
}

public interface IIncomeSourceRepository
{
    Task AddAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default);
    Task UpdateAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default);
    Task<IncomeSource?> GetByIdAsync(IncomeId incomeId, CancellationToken cancellationToken = default);
}

public interface IPersonalBillRepository
{
    Task AddAsync(PersonalBill personalBill, CancellationToken cancellationToken = default);
    Task UpdateAsync(PersonalBill personalBill, CancellationToken cancellationToken = default);
    Task<PersonalBill?> GetByIdAsync(PersonalBillId id, CancellationToken cancellationToken = default);
}

public interface IHouseholdCoverageEngine
{
    CoverageStatusResponse BuildCoverageStatus(
        Guid householdId,
        Money totalIncome,
        Money totalBills,
        DateTime periodStart,
        DateTime periodEnd);
}
