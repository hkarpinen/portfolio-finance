using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, v => new ExpenseId(v));

        // Whose books this lands in. group_id and user_id used to say it between them, with
        // user_id meaning two different things depending on the other.
        builder.Ignore(b => b.GroupId);
        builder.ComplexProperty(b => b.Owner, owner =>
        {
            owner.Property(o => o.Kind).HasColumnName("owner_kind").HasConversion<int>().IsRequired();
            owner.Property(o => o.Id).HasColumnName("owner_id").IsRequired();
        });

        builder.Property(b => b.EnteredBy)
            .HasColumnName("entered_by")
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(b => b.RecurringExpenseId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new RecurringExpenseId(v.Value) : (RecurringExpenseId?)null)
            .IsRequired(false);

        builder.Property(b => b.OccurrenceDate).IsRequired();
        builder.Property(b => b.FundingAccountId);

        // One occurrence cannot be generated twice. Filtered, because a directly-entered expense
        // has no schedule and any number of those may share a date.
        builder.HasIndex(b => new { b.RecurringExpenseId, b.OccurrenceDate })
            .IsUnique()
            .HasFilter("recurring_expense_id IS NOT NULL");

        // The IDENTITY userId of the person who fronted the bill — not a membership id. Null on personal.
        builder.Property(b => b.PayerUserId).IsRequired(false);

        builder.Property(b => b.FundingSource).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(b => b.Title).HasMaxLength(300).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(b => b.DueDate).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        // Lands in the WHERE clause of every update, which is what serialises concurrent share
        // writes against their expense. An explicit column rather than Postgres' xmin: xmin is a
        // system column that already exists, and the provider tries to CREATE it.
        builder.Property(b => b.Version).IsConcurrencyToken().IsRequired();
        builder.Property(b => b.IsActive).IsRequired();

        builder.ComplexProperty(b => b.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        // Indexed on the owner columns in the migration with raw SQL: EF 8 cannot name a complex
        // property's columns in HasIndex, and the index is on how rows are actually looked up.
        builder.HasIndex(b => b.DueDate);
    }
}
