using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Infrastructure.Configurations.Academics;

public class ClassSubjectConfiguration : IEntityTypeConfiguration<ClassSubject>
{
    public void Configure(EntityTypeBuilder<ClassSubject> builder)
    {
        builder.ToTable("ClassSubjects");
        builder.HasKey(x => new { x.ClassId, x.SubjectId });
        builder.HasOne(x => x.Class).WithMany(x => x.Subjects).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
