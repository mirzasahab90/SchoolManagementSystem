namespace SchoolManagementSystem.Application.Services.Attendance;

/// <summary>% = (Present + Late) / (marked working days − Leave) × 100, one decimal; null when nothing countable.</summary>
public static class AttendanceMath
{
    public static decimal? Pct(int present, int late, int markedDays, int leave)
    {
        var denominator = markedDays - leave;
        return denominator <= 0 ? null : Math.Round((present + late) * 100m / denominator, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Staff: a half day counts 0.5 attended.</summary>
    public static decimal? StaffPct(int present, int late, int halfDay, int markedDays, int leave)
    {
        var denominator = markedDays - leave;
        return denominator <= 0 ? null : Math.Round((present + late + halfDay * 0.5m) * 100m / denominator, 1, MidpointRounding.AwayFromZero);
    }

    public static string Code(Domain.Entities.Attendance.StudentAttendanceStatus s) => s switch
    {
        Domain.Entities.Attendance.StudentAttendanceStatus.Present => "P",
        Domain.Entities.Attendance.StudentAttendanceStatus.Absent => "A",
        Domain.Entities.Attendance.StudentAttendanceStatus.Late => "L",
        _ => "Lv"
    };
}
