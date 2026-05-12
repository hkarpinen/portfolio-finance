using Finance.Domain.Aggregates;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class FinancialConnectionConfiguration : IEntityTypeConfiguration<FinancialConnection>
{
    public void Configure(EntityTypeBuilder<FinancialConnection> builder)
    {
        builder.ToTable("financial_connections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, v => new FinancialConnectionId(v));
        builder.Property(c => c.UserId).HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(c => c.ExternalId).HasColumnName("external_id").HasMaxLength(100).IsRequired();
        builder.Property(c => c.InstitutionName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.InstitutionId).HasMaxLength(100);
        builder.Property(c => c.EncryptedAccessToken).HasColumnType("text").IsRequired();
        builder.Property(c => c.Cursor).HasColumnType("text");
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(c => c.LastSyncedAt);
        builder.Property(c => c.LastWebhookAt);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => c.ExternalId).IsUnique();
        builder.HasIndex(c => c.UserId);
    }
}

internal sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.ToTable("financial_accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FinancialConnectionId).HasConversion(id => id.Value, v => new FinancialConnectionId(v));
        builder.Property(a => a.UserId).HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(a => a.ExternalAccountId).HasColumnName("external_account_id").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.OfficialName).HasMaxLength(300);
        builder.Property(a => a.Mask).HasMaxLength(20);
        builder.Property(a => a.Type).HasMaxLength(40).IsRequired();
        builder.Property(a => a.Subtype).HasMaxLength(40);
        builder.Property(a => a.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(a => a.CurrentBalance).HasPrecision(18, 2);
        builder.Property(a => a.AvailableBalance).HasPrecision(18, 2);

        builder.HasIndex(a => new { a.FinancialConnectionId, a.ExternalAccountId }).IsUnique();
        builder.HasIndex(a => a.UserId);

        builder.HasOne<FinancialConnection>()
            .WithMany()
            .HasForeignKey(a => a.FinancialConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("financial_transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.FinancialConnectionId).HasConversion(id => id.Value, v => new FinancialConnectionId(v));
        builder.Property(t => t.UserId).HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(t => t.ExternalTransactionId).HasColumnName("external_transaction_id").HasMaxLength(100).IsRequired();
        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.AuthorizedDate);
        builder.Property(t => t.Name).HasMaxLength(500).IsRequired();
        builder.Property(t => t.MerchantName).HasMaxLength(300);
        builder.Property(t => t.PrimaryCategory).HasMaxLength(100);
        builder.Property(t => t.DetailedCategory).HasMaxLength(200);
        builder.Property(t => t.Pending).IsRequired();
        builder.Property(t => t.LinkedEntityId);
        builder.Property(t => t.LinkedEntityType).HasMaxLength(60);

        builder.ComplexProperty(t => t.Amount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(t => t.ExternalTransactionId).IsUnique();
        builder.HasIndex(t => new { t.FinancialConnectionId, t.Date });
        builder.HasIndex(t => t.UserId);

        builder.HasOne<FinancialConnection>()
            .WithMany()
            .HasForeignKey(t => t.FinancialConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RecurringSuggestionConfiguration : IEntityTypeConfiguration<RecurringSuggestion>
{
    public void Configure(EntityTypeBuilder<RecurringSuggestion> builder)
    {
        builder.ToTable("recurring_suggestions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.FinancialConnectionId).HasConversion(id => id.Value, v => new FinancialConnectionId(v));
        builder.Property(s => s.UserId).HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(s => s.ExternalStreamId).HasColumnName("external_stream_id").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500).IsRequired();
        builder.Property(s => s.MerchantName).HasMaxLength(300);
        builder.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(s => s.FirstDate).IsRequired();
        builder.Property(s => s.LastDate).IsRequired();
        builder.Property(s => s.PredictedNextDate);
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.IsLinked).IsRequired();
        builder.Property(s => s.LinkedEntityId);
        builder.Property(s => s.LinkedEntityType).HasMaxLength(60);

        builder.ComplexProperty(s => s.AverageAmount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("average_amount").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("average_currency").HasMaxLength(3).IsRequired();
        });
        builder.ComplexProperty(s => s.LastAmount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("last_amount").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("last_currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(s => s.ExternalStreamId).IsUnique();
        builder.HasIndex(s => s.UserId);

        builder.HasOne<FinancialConnection>()
            .WithMany()
            .HasForeignKey(s => s.FinancialConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
