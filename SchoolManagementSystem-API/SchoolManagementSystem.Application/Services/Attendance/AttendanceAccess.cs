using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Services.Attendance;

/// <summary>Who may mark a section: admins (attendance:Edit with sections:Edit or staff:Edit) or the section's class teacher.</summary>
public class AttendanceAccess(SchoolManagementSystemDbContext db, ICurrentUser currentUser, IPermissionChecker permissions)
{
    public const int ClassTeacherWindowDays = 2;
    public const string NotClassTeacher = "Only the class teacher or an admin can mark this section.";
    public const string OutsideWindow = "You can mark this section for today and the last 2 days only.";

    public record AccessResult(bool CanEdit, bool IsAdmin, bool IsClassTeacher, string? LockedReason);

    private bool? _isAdmin;

    private async Task<bool> Can(string module, string action, CancellationToken ct) =>
        currentUser.UserId is int uid && await permissions.HasPermissionAsync(uid, module, action, ct);

    /// <summary>attendance:Edit together with sections:Edit or staff:Edit (Campus Admin, Principal).</summary>
    public async Task<bool> IsAdminAsync(CancellationToken ct = default) =>
        _isAdmin ??= await Can(AppModules.Attendance, AppActions.Edit, ct)
                     && (await Can(AppModules.Sections, AppActions.Edit, ct) || await Can(AppModules.Staff, AppActions.Edit, ct));

    /// <summary>
    /// True for users who can mark attendance but are not admins (class teachers): they read only their own
    /// sections. View-only roles (e.g. the office) and admins read the whole campus.
    /// </summary>
    public async Task<bool> IsRestrictedAsync(CancellationToken ct = default) =>
        !await IsAdminAsync(ct)
        && (await Can(AppModules.Attendance, AppActions.Add, ct) || await Can(AppModules.Attendance, AppActions.Edit, ct));

    public async Task<bool> CanSeeSectionAsync(Section section, CancellationToken ct = default) =>
        !await IsRestrictedAsync(ct) || (await MyStaffIdAsync(ct) is int me && section.ClassTeacherStaffId == me);

    /// <summary>The caller's own staff record (linked login, still working), if any.</summary>
    public Task<int?> MyStaffIdAsync(CancellationToken ct = default) =>
        currentUser.UserId is not int uid
            ? Task.FromResult<int?>(null)
            : db.Staff.AsNoTracking()
                .Where(s => s.UserId == uid && s.Status != StaffStatus.Resigned && s.Status != StaffStatus.Terminated)
                .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);

    public async Task<AccessResult> ResolveAsync(Section section, DateOnly date, CancellationToken ct = default)
    {
        var canWrite = await Can(AppModules.Attendance, AppActions.Add, ct) || await Can(AppModules.Attendance, AppActions.Edit, ct);
        if (await IsAdminAsync(ct)) return new AccessResult(true, true, false, null);

        var myStaff = await MyStaffIdAsync(ct);
        var isClassTeacher = myStaff != null && section.ClassTeacherStaffId == myStaff;
        if (!isClassTeacher || !canWrite) return new AccessResult(false, false, isClassTeacher, NotClassTeacher);

        var today = CalendarService.Today;
        if (date < today.AddDays(-ClassTeacherWindowDays)) return new AccessResult(false, false, true, OutsideWindow);
        return new AccessResult(true, false, true, null);
    }
}
