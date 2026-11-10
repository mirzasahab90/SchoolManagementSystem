using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Fees;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Infrastructure.Configurations.Fees;

public class FeeHeadConfiguration : IEntityTypeConfiguration<FeeHead>
{
    public void Configure(EntityTypeBuilder<FeeHead> b)
    {
        b.ToTable("FeeHeads");
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(x => new { x.CampusId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
    public void Configure(EntityTypeBuilder<FeeStructure> b)
    {
        b.ToTable("FeeStructures");
        b.Property(x => x.Amount).HasPrecision(10, 2);
        b.HasIndex(x => new { x.CampusId, x.AcademicSessionId, x.ClassId, x.FeeHeadId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne<Campus>().WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FeeHead).WithMany().HasForeignKey(x => x.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeeSettingsConfiguration : IEntityTypeConfiguration<FeeSettings>
{
    public void Configure(EntityTypeBuilder<FeeSettings> b)
    {
        b.ToTable("FeeSettings");
        b.Property(x => x.LateFine).HasPrecision(10, 2);
        b.Property(x => x.SiblingSecondPercent).HasPrecision(5, 2);
        b.Property(x => x.SiblingThirdPlusPercent).HasPrecision(5, 2);
        b.Property(x => x.StaffChildPercent).HasPrecision(5, 2);
        b.Property(x => x.ConcessionHeadIds).HasMaxLength(200);
        b.Property(x => x.BankName).HasMaxLength(100);
        b.Property(x => x.AccountTitle).HasMaxLength(100);
        b.Property(x => x.AccountNo).HasMaxLength(100);
        b.Property(x => x.ChallanNote).HasMaxLength(500);
        b.HasIndex(x => x.CampusId).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne<Campus>().WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentConcessionConfiguration : IEntityTypeConfiguration<StudentConcession>
{
    public void Configure(EntityTypeBuilder<StudentConcession> b)
    {
        b.ToTable("StudentConcessions");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.ValueType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Value).HasPrecision(10, 2);
        b.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.StudentId, x.AcademicSessionId, x.FeeHeadId, x.Kind }).IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
        b.HasOne<Campus>().WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FeeHead).WithMany().HasForeignKey(x => x.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Entities.Hiring.Staff>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeeVoucherConfiguration : IEntityTypeConfiguration<FeeVoucher>
{
    public void Configure(EntityTypeBuilder<FeeVoucher> b)
    {
        b.ToTable("FeeVouchers");
        b.Ignore(x => x.Balance);
        b.Ignore(x => x.IsOpen);
        b.Property(x => x.VoucherNo).HasMaxLength(50).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(x => x.Total).HasPrecision(10, 2);
        b.Property(x => x.LateFineAmount).HasPrecision(10, 2);
        b.Property(x => x.Paid).HasPrecision(10, 2);
        b.Property(x => x.CancelReason).HasMaxLength(200);
        b.HasIndex(x => x.VoucherNo).IsUnique();
        b.HasIndex(x => new { x.StudentId, x.Month }).IsUnique().HasFilter("[Status] <> 'Cancelled' AND [IsDeleted] = 0");
        b.HasIndex(x => new { x.CampusId, x.Month });
        b.HasOne<Campus>().WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Enrollment).WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademicSession>().WithMany().HasForeignKey(x => x.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FeeVoucher>().WithMany().HasForeignKey(x => x.CarriedToVoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeeVoucherLineConfiguration : IEntityTypeConfiguration<FeeVoucherLine>
{
    public void Configure(EntityTypeBuilder<FeeVoucherLine> b)
    {
        b.ToTable("FeeVoucherLines");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(12);
        b.Property(x => x.Description).HasMaxLength(120).IsRequired();
        b.Property(x => x.ConcessionNote).HasMaxLength(80);
        b.Property(x => x.Amount).HasPrecision(10, 2);
        b.Property(x => x.Concession).HasPrecision(10, 2);
        b.Property(x => x.Net).HasPrecision(10, 2);
        b.HasOne(x => x.FeeVoucher).WithMany(x => x.Lines).HasForeignKey(x => x.FeeVoucherId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<FeeHead>().WithMany().HasForeignKey(x => x.FeeHeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeePaymentConfiguration : IEntityTypeConfiguration<FeePayment>
{
    public void Configure(EntityTypeBuilder<FeePayment> b)
    {
        b.ToTable("FeePayments");
        b.Property(x => x.ReceiptNo).HasMaxLength(50).IsRequired();
        b.Property(x => x.Amount).HasPrecision(10, 2);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Reference).HasMaxLength(60);
        b.Property(x => x.VoidReason).HasMaxLength(200);
        b.HasIndex(x => x.ReceiptNo).IsUnique();
        b.HasIndex(x => new { x.CampusId, x.PaidOn });
        b.HasOne<Campus>().WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FeeVoucher).WithMany(x => x.Payments).HasForeignKey(x => x.FeeVoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}
