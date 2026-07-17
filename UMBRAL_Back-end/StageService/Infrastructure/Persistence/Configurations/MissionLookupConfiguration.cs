namespace StageService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageService.Domain.MissionLookup;

public class MissionLookupConfiguration : IEntityTypeConfiguration<MissionLookup>
{
    public void Configure(EntityTypeBuilder<MissionLookup> builder)
    {
        builder.ToTable("MissionsLookup");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Status).IsRequired().HasMaxLength(50);
    }
}
