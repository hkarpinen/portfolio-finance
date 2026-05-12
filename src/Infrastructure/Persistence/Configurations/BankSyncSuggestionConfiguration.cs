using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class BankSyncSuggestionConfiguration : IEntityTypeConfiguration<BankSyncSuggestion>
{
    public void Configure(EntityTypeBuilder<BankSyncSuggestion> builder)
    {
        builder.ToTable("bank_sync_suggestions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ConnectionId)
            .HasConversion(id => id.Value, v => new FinancialConnectionId(v))
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(s => s.ExternalTransactionId).HasMaxLength(500).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(500).IsRequired();
        builder.Property(s => s.MerchantName).HasMaxLength(500);
        builder.Property(s => s.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.Direction).HasMaxLength(10).IsRequired();
        builder.Property(s => s.TransactionDate).IsRequired();
        builder.Property(s => s.LinkedEntityType).HasMaxLength(100);

        // One suggestion per transaction — prevents duplicates across syncs.
        builder.HasIndex(s => s.ExternalTransactionId).IsUnique();
        builder.HasIndex(s => new { s.UserId, s.Dismissed });
    }
}
