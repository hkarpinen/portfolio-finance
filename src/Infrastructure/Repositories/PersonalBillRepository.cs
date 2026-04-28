using Bills.Application.Managers.Dependencies;
using Bills.Domain.Aggregates;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class PersonalBillRepository : IPersonalBillRepository
{
    private readonly BillsDbContext _dbContext;

    public PersonalBillRepository(BillsDbContext dbContext) => _dbContext = dbContext;

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
