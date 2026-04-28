using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", "bills");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(m => m.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.Published)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.LastError)
            .HasMaxLength(2048);

        builder.Property(m => m.DeadLettered)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(m => new { m.Published, m.DeadLettered })
            .HasFilter("published = false AND dead_lettered = false");
    }
}
