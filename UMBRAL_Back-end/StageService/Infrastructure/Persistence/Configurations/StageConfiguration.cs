namespace StageService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageService.Domain.Stages;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Title).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.MissionId).IsRequired();
        builder.Property(s => s.Order).IsRequired();
        builder.Property(s => s.BaseScore).IsRequired();
        builder.Property(s => s.Question).HasMaxLength(2000);
        builder.Property(s => s.QrCode).HasMaxLength(500);

        builder.HasMany(s => s.Options)
            .WithOne()
            .HasForeignKey(o => o.StageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
