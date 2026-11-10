using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagementSystem.Domain.Entities.Attendance;
using SchoolManagementSystem.Infrastructure.Configurations.Hiring;

namespace SchoolManagementSystem.Infrastructure.Configurations.Attendance;

public class CampusCalendarConfiguration : IEntityTypeConfiguration<CampusCalendar>
{
    public void Configure(EntityTypeBuilder<CampusCalendar> b)
    {
        b.ToTable("CampusCalendars");
        b.Property(x => x.WeeklyOff).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.CampusId).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("Holidays");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.CampusId, x.StartDate });
        b.HasOne(x => x.Campus).WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> b)
    {
        b.ToTable("AttendanceDays");
        b.Property(x => x.MarkedOn).HasConversion(Utc.Converter);
        // One day per section and date; a racing first save surfaces as 409 through the global handler.
        b.HasIndex(x => new { x.SectionId, x.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.CampusId, x.Date });
        b.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
    public void Configure(EntityTypeBuilder<AttendanceEntry> b)
    {
        b.ToTable("AttendanceEntries");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasIndex(x => new { x.AttendanceDayId, x.StudentId }).IsUnique();
        b.HasIndex(x => x.StudentId);
        b.HasOne(x => x.AttendanceDay).WithMany(x => x.Entries).HasForeignKey(x => x.AttendanceDayId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffAttendanceConfiguration : IEntityTypeConfiguration<StaffAttendance>
{
    public void Configure(EntityTypeBuilder<StaffAttendance> b)
    {
        b.ToTable("StaffAttendance");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasIndex(x => new { x.StaffId, x.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.CampusId, x.Date });
        b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
