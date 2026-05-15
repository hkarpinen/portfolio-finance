using Finance.Domain;
using Finance.Domain.Aggregates;
using Finance.Domain.Aggregates;
using Finance.Infrastructure.Persistence.Projections;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class FinanceDbContext : DbContext
{
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpensePayment> ExpensePayments => Set<ExpensePayment>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<ExpenseSplitPayment> ExpenseSplitPayments => Set<ExpenseSplitPayment>();
    public DbSet<UserProjection> UserProjections => Set<UserProjection>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // Financial connection cluster (Plaid-backed).
    public DbSet<FinancialConnection> FinancialConnections => Set<FinancialConnection>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<RecurringSuggestion> RecurringSuggestions => Set<RecurringSuggestion>();
    public DbSet<BankSyncSuggestion> BankSyncSuggestions => Set<BankSyncSuggestion>();

    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options) { }

    /// <summary>
    /// Drains domain events from every tracked aggregate root into the outbox
    /// before flushing to the database — outbox row and aggregate row are written
    /// in the same transaction so there is no window for event loss.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DrainDomainEventsToOutbox();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void DrainDomainEventsToOutbox()
    {
        var aggregatesWithEvents = ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.GetDomainEvents().Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.GetDomainEvents())
                this.AddToOutbox(domainEvent);
            aggregate.ClearDomainEvents();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
    }
}
