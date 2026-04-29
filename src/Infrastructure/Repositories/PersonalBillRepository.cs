using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class PersonalBillRepository : IPersonalBillRepository
{
    private readonly FinanceDbContext _dbContext;

    public PersonalBillRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(PersonalBill personalBill, CancellationToken cancellationToken = default)
    {
        await _dbContext.PersonalBills.AddAsync(personalBill, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PersonalBill personalBill, CancellationToken cancellationToken = default)
    {
        _dbContext.PersonalBills.Update(personalBill);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PersonalBill?> GetByIdAsync(PersonalBillId id, CancellationToken cancellationToken = default)
        => _dbContext.PersonalBills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
}
