using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Infrastructure.Configurations.Academics;

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.Property(x => x.Name).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Room).HasMaxLength(50);
        builder.Property(x => x.Shift).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.CampusId, x.AcademicSessionId, x.ClassId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClassTeacher).WithMany().HasForeignKey(x => x.ClassTeacherStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
