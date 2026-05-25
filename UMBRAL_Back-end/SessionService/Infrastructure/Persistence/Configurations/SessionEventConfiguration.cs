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

        // Composite index for efficient "recent events for session" queries
        builder.HasIndex(e => new { e.SessionId, e.OccurredAt });
    }
}
