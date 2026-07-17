namespace SessionService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionService.Domain.Sessions;

public class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
{
    public void Configure(EntityTypeBuilder<SessionEvent> builder)
    {
        builder.ToTable("SessionEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.OccurredAt).IsRequired();

        builder.Property(e => e.ActorName)
            .IsRequired()
            .HasMaxLength(100);

        // HU-26: technical metadata for the command audit log. Nullable so the
        // existing rows from HU-22 don't need backfill.
        builder.Property(e => e.CommandType)
            .HasMaxLength(80);

        builder.Property(e => e.Outcome)
            .HasMaxLength(20);

        // Composite index for efficient "recent events for session" queries
        builder.HasIndex(e => new { e.SessionId, e.OccurredAt });
    }
}
