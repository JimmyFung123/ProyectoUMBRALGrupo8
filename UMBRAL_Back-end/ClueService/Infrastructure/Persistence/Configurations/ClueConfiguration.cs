namespace ClueService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClueService.Domain.Clues;

public class ClueConfiguration : IEntityTypeConfiguration<Clue>
{
    public void Configure(EntityTypeBuilder<Clue> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Content).IsRequired(false).HasMaxLength(1000);
        builder.Property(c => c.StageId).IsRequired();
        builder.Property(c => c.MissionId).IsRequired();
        builder.Property(c => c.StageType).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.AutoReleaseAfterMinutes).IsRequired(false);
        builder.Property(c => c.Latitude).IsRequired(false);
        builder.Property(c => c.Longitude).IsRequired(false);
        builder.Property(c => c.RadiusMeters).IsRequired(false);
    }
}
