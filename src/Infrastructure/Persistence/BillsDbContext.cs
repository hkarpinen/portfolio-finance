using Bills.Domain.Aggregates;
using Bills.Domain.ReadModels;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class BillsDbContext : DbContext
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillSplit> BillSplits => Set<BillSplit>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<PersonalBill> PersonalBills => Set<PersonalBill>();
    public DbSet<UserProjection> UserProjections => Set<UserProjection>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public BillsDbContext(DbContextOptions<BillsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bills");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillsDbContext).Assembly);
    }
}
