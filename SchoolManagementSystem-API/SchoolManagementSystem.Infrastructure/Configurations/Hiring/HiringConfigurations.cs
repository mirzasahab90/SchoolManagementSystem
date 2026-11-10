using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Infrastructure.Configurations.Hiring;

/// <summary>Stored as UTC; read back with Kind = Utc so JSON carries the "Z" and browsers show local time correctly.</summary>
internal static class Utc
{
    public static readonly ValueConverter<DateTime, DateTime> Converter =
        new(v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> NullableConverter =
        new(v => v == null ? v : v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime(),
            v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
}

public class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> b)
    {
        b.ToTable("Vacancies");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Designation).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ClassIds).HasMaxLength(500);
        b.Property(x => x.SubjectIds).HasMaxLength(500);
        b.Property(x => x.Requirements).HasMaxLength(2000);
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.ToTable("Candidates");
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Cnic).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Qualification).HasMaxLength(200);
        b.Property(x => x.ExperienceYears).HasPrecision(4, 1);
        b.Property(x => x.CurrentSalary).HasPrecision(12, 2);
        b.Property(x => x.ExpectedSalary).HasPrecision(12, 2);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.StageReason).HasMaxLength(500);
        b.Property(x => x.CvPath).HasMaxLength(300);
        b.Property(x => x.CvFileName).HasMaxLength(255);
        b.HasIndex(x => new { x.VacancyId, x.Cnic }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Cnic] IS NOT NULL");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vacancy).WithMany(x => x.Candidates).HasForeignKey(x => x.VacancyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CandidateStageHistoryConfiguration : IEntityTypeConfiguration<CandidateStageHistory>
{
    public void Configure(EntityTypeBuilder<CandidateStageHistory> b)
    {
        b.ToTable("CandidateStageHistory");
        b.Property(x => x.FromStage).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ToStage).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.OccurredOn).HasConversion(Utc.Converter);
        b.HasIndex(x => x.CandidateId);
    }
}

public class InterviewRoundConfiguration : IEntityTypeConfiguration<InterviewRound>
{
    public void Configure(EntityTypeBuilder<InterviewRound> b)
    {
        b.ToTable("InterviewRounds");
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.Result).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.ScheduledAt).HasConversion(Utc.Converter);
        b.HasOne(x => x.Candidate).WithMany(x => x.Interviews).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Interviewer).WithMany().HasForeignKey(x => x.InterviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.ToTable("Offers");
        b.Property(x => x.Designation).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.OfferedSalary).HasPrecision(12, 2);
        b.Property(x => x.RespondedOn).HasConversion(Utc.NullableConverter);
        b.HasOne(x => x.Candidate).WithMany(x => x.Offers).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> b)
    {
        b.ToTable("Staff");
        b.Ignore(x => x.FullName);
        b.Property(x => x.EmployeeNo).HasMaxLength(30).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Cnic).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Designation).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BasicSalary).HasPrecision(12, 2);
        b.HasIndex(x => new { x.CampusId, x.EmployeeNo }).IsUnique().HasFilter("[IsDeleted] = 0");
        // Only staff who have not left hold their CNIC, so a former member can be rehired.
        b.HasIndex(x => x.Cnic).IsUnique().HasFilter("[IsDeleted] = 0 AND [Cnic] IS NOT NULL AND [Status] <> 'Resigned' AND [Status] <> 'Terminated'");
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsDeleted] = 0 AND [UserId] IS NOT NULL");
        b.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.PhotoPath).HasMaxLength(300);
        b.Property(x => x.EmergencyName).HasMaxLength(150);
        b.Property(x => x.EmergencyRelation).HasMaxLength(50);
        b.Property(x => x.EmergencyPhone).HasMaxLength(50);
        b.Property(x => x.Qualification).HasMaxLength(200);
        b.Property(x => x.ExperienceYears).HasPrecision(4, 1);
        b.Property(x => x.PreviousEmployer).HasMaxLength(200);
        b.Property(x => x.BankName).HasMaxLength(100);
        b.Property(x => x.BankAccount).HasMaxLength(34);
        b.Property(x => x.LeaveReason).HasMaxLength(500);
        b.Property(x => x.ContractSignedOn).HasConversion(Utc.NullableConverter);
        b.Property(x => x.OrientationDoneOn).HasConversion(Utc.NullableConverter);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SourceCandidateId).IsUnique().HasFilter("[IsDeleted] = 0 AND [SourceCandidateId] IS NOT NULL");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceCandidate).WithMany().HasForeignKey(x => x.SourceCandidateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffDocumentConfiguration : IEntityTypeConfiguration<StaffDocument>
{
    public void Configure(EntityTypeBuilder<StaffDocument> b)
    {
        b.ToTable("StaffDocuments");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StoredPath).HasMaxLength(300).IsRequired();
        b.Property(x => x.UploadedOn).HasConversion(Utc.Converter);
        b.HasOne(x => x.Staff).WithMany(x => x.Documents).HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TeachingAssignmentConfiguration : IEntityTypeConfiguration<TeachingAssignment>
{
    public void Configure(EntityTypeBuilder<TeachingAssignment> b)
    {
        b.ToTable("TeachingAssignments");
        b.Property(x => x.EndReason).HasMaxLength(200);
        // One current teacher per section subject; a race surfaces as 409 through the global handler.
        b.HasIndex(x => new { x.SectionId, x.SubjectId }).IsUnique().HasFilter("[EndedOn] IS NULL AND [IsDeleted] = 0");
        b.HasIndex(x => x.StaffId);
        b.HasOne(x => x.Staff).WithMany(x => x.Assignments).HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AcademicSession).WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
