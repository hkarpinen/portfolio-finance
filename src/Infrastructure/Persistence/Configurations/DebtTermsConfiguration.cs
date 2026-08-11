using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class DebtTermsConfiguration : IEntityTypeConfiguration<DebtTerms>
{
    public void Configure(EntityTypeBuilder<DebtTerms> builder)
    {
        builder.ToTable("debt_terms", "finance");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.AccountId)
            .HasConversion(id => id.Value, v => AccountId.Create(v));
        // One set of terms per account: a card cannot be on two rates at once.
        builder.HasIndex(t => t.AccountId).IsUnique();

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => UserId.Create(v));
        builder.HasIndex(t => t.UserId);

        // A rate is a percentage to two places (24.99), not money.
        builder.Property(t => t.AnnualPercentageRate).HasPrecision(6, 3);

        // Currency comes from the ledger the account belongs to; see DebtTerms.
        builder.Property(t => t.CreditLimit).HasPrecision(18, 2);
        builder.Property(t => t.MinimumPayment).HasPrecision(18, 2);
    }
}
