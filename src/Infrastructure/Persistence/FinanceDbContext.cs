using Finance.Domain;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;
using Finance.Infrastructure.Persistence.Projections;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence.Configurations;
using MassTransit;
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
    public DbSet<MemberTransfer> MemberTransfers => Set<MemberTransfer>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<FinancialConnection> FinancialConnections => Set<FinancialConnection>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();

    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options) { }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);

        // MassTransit's transactional outbox and inbox.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
