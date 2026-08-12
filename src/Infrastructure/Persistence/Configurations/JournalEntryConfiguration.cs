using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new JournalEntryId(v));

        builder.Property(e => e.LedgerId)
            .HasConversion(id => id.Value, v => new LedgerId(v))
            .IsRequired();

        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(200);
        builder.Property(e => e.RecordedAt).IsRequired();
        builder.Property(e => e.PostedByUserId);

        builder.Property(e => e.ReversalOfEntryId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new JournalEntryId(v.Value) : (JournalEntryId?)null);

        builder.Property(e => e.ReversedByEntryId);

        // Postings live in their own table but belong to the JournalEntry aggregate — they can only be
        // created via JournalEntry.Post, so the mapping goes through the private backing field.
        builder.HasMany(typeof(Posting), "_postings")
            .WithOne()
            .HasForeignKey(nameof(Posting.EntryId))
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_postings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(e => e.Postings);

        builder.HasIndex(e => new { e.LedgerId, e.Date });

        // The settled-state read keys on (allocation, occurrence); charge postings are looked up by charge.
        builder.HasIndex(e => new { e.SourceAllocationId, e.SourceOccurrence });
        builder.HasIndex(e => e.SourceChargeId);

        // DB-level backstop against a duplicate ACTIVE posting under one source: at most one entry
        // per (ledger, source) may be neither a reversal nor itself reversed. A reverse+repost is
        // still allowed — the reversed original has reversed_by_entry_id set, so it drops out of the
        // filter. NOTE: HasFilter strings are NOT translated by the snake_case convention — column
        // names MUST be written in snake_case here or the migration fails at runtime.
        builder.HasIndex(e => new { e.LedgerId, e.Source })
            .IsUnique()
            .HasFilter("source IS NOT NULL AND reversal_of_entry_id IS NULL AND reversed_by_entry_id IS NULL");
    }
}

internal sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        builder.ToTable("postings");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new PostingId(v));

        builder.Property(p => p.EntryId)
            .HasConversion(id => id.Value, v => new JournalEntryId(v))
            .IsRequired();

        builder.Property(p => p.AccountId)
            .HasConversion(id => id.Value, v => new AccountId(v))
            .IsRequired();

        builder.Property(p => p.Direction).HasConversion<int>();

        builder.ComplexProperty(p => p.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(p => p.SignedAmount);

        builder.HasIndex(p => p.AccountId);
        builder.HasIndex(p => p.EntryId);
    }
}
