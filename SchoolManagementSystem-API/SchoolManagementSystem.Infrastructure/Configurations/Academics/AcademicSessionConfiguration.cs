using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Infrastructure.Configurations.Academics;

public class AcademicSessionConfiguration : IEntityTypeConfiguration<AcademicSession>
{
    public void Configure(EntityTypeBuilder<AcademicSession> builder)
    {
        builder.ToTable("AcademicSessions");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.CampusId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        // One current session per owner (HQ, or each campus).
        builder.HasIndex(x => x.CampusId).IsUnique().HasFilter("[IsCurrent] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("IX_AcademicSessions_CurrentPerOwner");
        builder.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}
