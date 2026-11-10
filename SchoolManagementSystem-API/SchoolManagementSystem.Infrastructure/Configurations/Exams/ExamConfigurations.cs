using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Exams;
using SchoolManagementSystem.Infrastructure.Configurations.Hiring;

namespace SchoolManagementSystem.Infrastructure.Configurations.Exams;

public class GradeScaleConfiguration : IEntityTypeConfiguration<GradeScale>
{
    public void Configure(EntityTypeBuilder<GradeScale> b)
    {
        b.ToTable("GradeScales");
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.CampusId).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GradeBandConfiguration : IEntityTypeConfiguration<GradeBand>
{
    public void Configure(EntityTypeBuilder<GradeBand> b)
    {
        b.ToTable("GradeBands");
        b.Property(x => x.Grade).HasMaxLength(5).IsRequired();
        b.Property(x => x.MinPercent).HasPrecision(5, 2);
        b.HasIndex(x => new { x.GradeScaleId, x.Grade }).IsUnique();
        b.HasOne(x => x.GradeScale).WithMany(x => x.Bands).HasForeignKey(x => x.GradeScaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ResultSchemeConfiguration : IEntityTypeConfiguration<ResultScheme>
{
    public void Configure(EntityTypeBuilder<ResultScheme> b)
    {
        b.ToTable("ResultSchemes");
        b.HasIndex(x => new { x.CampusId, x.AcademicSessionId, x.ClassId });
    }
}

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> b)
    {
        b.ToTable("Exams");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(x => new { x.CampusId, x.AcademicSessionId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExamPaperConfiguration : IEntityTypeConfiguration<ExamPaper>
{
    public void Configure(EntityTypeBuilder<ExamPaper> b)
    {
        b.ToTable("ExamPapers");
        b.Ignore(x => x.TheoryMarks);
        b.Property(x => x.TotalMarks).HasPrecision(6, 2);
        b.Property(x => x.PassMarks).HasPrecision(6, 2);
        b.Property(x => x.PracticalMarks).HasPrecision(6, 2);
        b.HasIndex(x => new { x.ExamId, x.ClassId, x.SubjectId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Exam).WithMany(x => x.Papers).HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaperSectionConfiguration : IEntityTypeConfiguration<PaperSection>
{
    public void Configure(EntityTypeBuilder<PaperSection> b)
    {
        b.ToTable("PaperSections");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.SubmittedOn).HasConversion(Utc.NullableConverter);
        b.HasIndex(x => new { x.ExamPaperId, x.SectionId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.ExamPaper).WithMany().HasForeignKey(x => x.ExamPaperId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MarkConfiguration : IEntityTypeConfiguration<Mark>
{
    public void Configure(EntityTypeBuilder<Mark> b)
    {
        b.ToTable("Marks");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Theory).HasPrecision(6, 2);
        b.Property(x => x.Practical).HasPrecision(6, 2);
        b.HasIndex(x => new { x.ExamPaperId, x.StudentId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.ExamPaperId, x.SectionId });
        b.HasOne(x => x.ExamPaper).WithMany().HasForeignKey(x => x.ExamPaperId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SectionResultConfiguration : IEntityTypeConfiguration<SectionResult>
{
    public void Configure(EntityTypeBuilder<SectionResult> b)
    {
        b.ToTable("SectionResults");
        b.Property(x => x.PublishedOn).HasConversion(Utc.NullableConverter);
        b.HasIndex(x => new { x.ExamId, x.SectionId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Exam).WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentResultConfiguration : IEntityTypeConfiguration<StudentResult>
{
    public void Configure(EntityTypeBuilder<StudentResult> b)
    {
        b.ToTable("StudentResults");
        b.Property(x => x.Obtained).HasPrecision(7, 2);
        b.Property(x => x.Total).HasPrecision(7, 2);
        b.Property(x => x.Percent).HasPrecision(5, 2);
        b.Property(x => x.Grade).HasMaxLength(5);
        b.HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.ExamId, x.SectionId });
    }
}

public class ResultRemarkConfiguration : IEntityTypeConfiguration<ResultRemark>
{
    public void Configure(EntityTypeBuilder<ResultRemark> b)
    {
        b.ToTable("ResultRemarks");
        b.Property(x => x.Remark).HasMaxLength(300).IsRequired();
        b.HasIndex(x => new { x.ExamId, x.AcademicSessionId, x.StudentId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
