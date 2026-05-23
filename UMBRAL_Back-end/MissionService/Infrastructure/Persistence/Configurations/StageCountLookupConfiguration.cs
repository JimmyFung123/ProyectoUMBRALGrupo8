namespace UMBRAL_Back_end.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMBRAL_Back_end.Domain.Missions;

public class StageCountLookupConfiguration : IEntityTypeConfiguration<StageCountLookup>
{
    public void Configure(EntityTypeBuilder<StageCountLookup> builder)
    {
        builder.ToTable("StageCountLookup");
        builder.HasKey(s => s.MissionId);
        builder.Property(s => s.Count).IsRequired();
    }
}
