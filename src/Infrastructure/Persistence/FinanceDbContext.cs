using Finance.Domain;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;
using Finance.Infrastructure.Persistence.Projections;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class FinanceDbContext : DbContext
{
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<Share> Shares => Set<Share>();

    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<DebtTerms> DebtTerms => Set<DebtTerms>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<UserProjection> UserProjections => Set<UserProjection>();
    public DbSet<GroupMemberProjection> GroupMemberProjections => Set<GroupMemberProjection>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<MemberTransfer> MemberTransfers => Set<MemberTransfer>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<FinancialConnection> FinancialConnections => Set<FinancialConnection>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<RecurringSuggestion> RecurringSuggestions => Set<RecurringSuggestion>();
    public DbSet<BankSyncSuggestion> BankSyncSuggestions => Set<BankSyncSuggestion>();

    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options) { }

    // Drains domain events from every tracked aggregate root into the outbox BEFORE flushing, so the
    // outbox row and the aggregate row are written in one transaction and no event can be lost.
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
