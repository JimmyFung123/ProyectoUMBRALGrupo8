namespace UMBRAL_Back_end.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMBRAL_Back_end.Domain.Missions;

public class ClueConfiguration : IEntityTypeConfiguration<Clue>
{
    public void Configure(EntityTypeBuilder<Clue> builder)
    {
        builder.ToTable("Clues");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Order)
            .IsRequired();

        // Trivia-specific (nullable for TreasureHunt)
        builder.Property(c => c.Content)
            .HasMaxLength(2000);

        // TreasureHunt-specific (nullable for Trivia)
        builder.Property(c => c.Latitude);
        builder.Property(c => c.Longitude);
        builder.Property(c => c.RadiusMeters);

        // Unique order per stage at DB level
        builder.HasIndex(c => new { c.StageId, c.Order })
            .IsUnique();
    }
}
