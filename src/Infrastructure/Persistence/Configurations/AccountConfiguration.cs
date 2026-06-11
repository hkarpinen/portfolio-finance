using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, v => new AccountId(v));

        builder.Property(a => a.LedgerId)
            .HasConversion(id => id.Value, v => new LedgerId(v))
            .IsRequired();

        builder.Property(a => a.Code).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.AccountType).HasConversion<int>();

        builder.Property(a => a.ParentAccountId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new AccountId(v.Value) : (AccountId?)null);

        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        // NormalBalance is derived (AccountType) — not stored.
        builder.Ignore(a => a.NormalBalance);

        builder.HasIndex(a => a.LedgerId);
        builder.HasIndex(a => new { a.LedgerId, a.Code }).IsUnique();
    }
}
