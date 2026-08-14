using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("recurring_expenses");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => new RecurringExpenseId(v));

        // group_id becomes the owner's id and a kind column beside it; a row that had no group
        // was always somebody's own.
        builder.Ignore(s => s.GroupId);
        builder.ComplexProperty(s => s.Owner, owner =>
        {
            owner.Property(o => o.Kind).HasColumnName("owner_kind").HasConversion<int>().IsRequired();
            owner.Property(o => o.Id).HasColumnName("owner_id").IsRequired();
        });

        builder.Property(s => s.CreatedBy).HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(s => s.Currency).IsRequired().HasMaxLength(3);
        builder.Property(s => s.Title).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.FundingSource).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Amount is derived from the versions, never stored — the same rule the ledger follows
        // for balances, for the same reason: a stored copy can disagree with what produced it.
        builder.Ignore(s => s.Amount);

        builder.OwnsMany(s => s.Amounts, a =>
        {
            a.ToTable("recurring_expense_terms");
            a.WithOwner().HasForeignKey("recurring_expense_id");
            a.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            a.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            a.HasKey("recurring_expense_id", nameof(RecurringExpenseTerm.EffectiveFrom));
        });
        builder.Navigation(s => s.Amounts).AutoInclude();

        // The anchor and interval. Nothing here is a date an expense exists on — those are computed.
        builder.OwnsOne(s => s.Recurrence, r =>
        {
            r.Property(x => x.Frequency).HasColumnName("frequency").HasConversion<string>().HasMaxLength(50).IsRequired();
            r.Property(x => x.StartDate).HasColumnName("anchor_date").IsRequired();
            r.Property(x => x.EndDate).HasColumnName("end_date");
        });
        builder.Navigation(s => s.Recurrence).IsRequired();

        // Indexed on the owner columns in the migration with raw SQL: EF 8 cannot name a complex
        // property's columns in HasIndex, and the index is on how rows are actually looked up.
        builder.HasIndex(s => s.CreatedBy);

    }
}
