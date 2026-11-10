using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Infrastructure.Configurations.Students;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.ToTable("Students");
        b.Ignore(x => x.FullName);
        b.Property(x => x.AdmissionNo).HasMaxLength(30).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BFormNo).HasMaxLength(20);
        b.Property(x => x.BloodGroup).HasMaxLength(20);
        b.Property(x => x.Religion).HasMaxLength(50);
        b.Property(x => x.Nationality).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.PreviousSchool).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.PhotoPath).HasMaxLength(300);
        b.Property(x => x.LeaveReason).HasMaxLength(500);
        b.HasIndex(x => new { x.CampusId, x.AdmissionNo }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.BFormNo).IsUnique().HasFilter("[IsDeleted] = 0 AND [BFormNo] IS NOT NULL");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> b)
    {
        b.ToTable("Guardians");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Cnic).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        b.Property(x => x.AltPhone).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Occupation).HasMaxLength(100);
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasIndex(x => x.Cnic).IsUnique().HasFilter("[IsDeleted] = 0 AND [Cnic] IS NOT NULL");
    }
}

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> b)
    {
        b.ToTable("StudentGuardians");
        b.HasKey(x => new { x.StudentId, x.GuardianId });
        b.Property(x => x.Relation).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Student).WithMany(x => x.Guardians).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Guardian).WithMany(x => x.Students).HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("Enrollments");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.StudentId, x.AcademicSessionId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.SectionId, x.RollNo }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] = 'Active'");
        b.HasOne(x => x.Student).WithMany(x => x.Enrollments).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EnrollmentHistoryConfiguration : IEntityTypeConfiguration<EnrollmentHistory>
{
    public void Configure(EntityTypeBuilder<EnrollmentHistory> b)
    {
        b.ToTable("EnrollmentHistory");
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.StudentId);
    }
}

public class StudentDocumentConfiguration : IEntityTypeConfiguration<StudentDocument>
{
    public void Configure(EntityTypeBuilder<StudentDocument> b)
    {
        b.ToTable("StudentDocuments");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StoredPath).HasMaxLength(300).IsRequired();
        b.HasOne(x => x.Student).WithMany(x => x.Documents).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CampusSequenceConfiguration : IEntityTypeConfiguration<CampusSequence>
{
    public void Configure(EntityTypeBuilder<CampusSequence> b)
    {
        b.ToTable("CampusSequences");
        b.HasKey(x => new { x.CampusId, x.Key });
        b.Property(x => x.Key).HasMaxLength(30);
    }
}
