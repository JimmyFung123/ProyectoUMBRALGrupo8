namespace ClueService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClueService.Domain.StageLookup;

public class StageLookupConfiguration : IEntityTypeConfiguration<StageLookup>
{
    public void Configure(EntityTypeBuilder<StageLookup> builder)
    {
        builder.ToTable("StagesLookup");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.MissionId).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
    }
}
