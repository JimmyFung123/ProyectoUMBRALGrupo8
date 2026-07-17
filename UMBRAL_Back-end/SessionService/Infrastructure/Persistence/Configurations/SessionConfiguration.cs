namespace SessionService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionService.Domain.Sessions;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.MissionId).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.HasIndex(s => s.MissionId);
        builder.HasIndex(s => s.Status);
        builder.Property(s => s.AccessCode).IsRequired().HasMaxLength(10);
        builder.HasIndex(s => s.AccessCode).IsUnique();
        builder.Property(s => s.CreatedByOperatorId).HasMaxLength(64);
        builder.HasIndex(s => s.CreatedByOperatorId);
    }
}
