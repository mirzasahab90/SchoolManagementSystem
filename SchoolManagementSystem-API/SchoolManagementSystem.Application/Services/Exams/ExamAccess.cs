using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Services.Exams;

/// <summary>Admins (module Edit + sections/staff Edit), subject teachers (current assignment) and class teachers.</summary>
public class ExamAccess(SchoolManagementSystemDbContext db, ICurrentUser currentUser, IPermissionChecker permissions)
{
    private readonly Dictionary<string, bool> _admin = new();
    private int? _me;
    private bool _meLoaded;

    public async Task<bool> CanAsync(string module, string action, CancellationToken ct = default) =>
        currentUser.UserId is int uid && await permissions.HasPermissionAsync(uid, module, action, ct);

    public async Task<bool> IsAdminAsync(string module, CancellationToken ct = default)
    {
        if (_admin.TryGetValue(module, out var cached)) return cached;
        var ok = await CanAsync(module, AppActions.Edit, ct)
                 && (await CanAsync(AppModules.Sections, AppActions.Edit, ct) || await CanAsync(AppModules.Staff, AppActions.Edit, ct));
        _admin[module] = ok;
        return ok;
    }

    public async Task<int?> MyStaffIdAsync(CancellationToken ct = default)
    {
        if (_meLoaded) return _me;
        _meLoaded = true;
        if (currentUser.UserId is int uid)
        {
            _me = await db.Staff.AsNoTracking()
                .Where(s => s.UserId == uid && s.Status != StaffStatus.Resigned && s.Status != StaffStatus.Terminated)
                .Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        }

        return _me;
    }

    /// <summary>The caller holds a current teaching assignment for this section and subject.</summary>
    public async Task<bool> TeachesAsync(int sectionId, int subjectId, CancellationToken ct = default) =>
        await MyStaffIdAsync(ct) is int me
        && await db.TeachingAssignments.AnyAsync(a => a.StaffId == me && a.SectionId == sectionId && a.SubjectId == subjectId && a.EndedOn == null, ct);

    /// <summary>Admins see every section; others the sections they are class teacher of or teach a subject in.</summary>
    public async Task<bool> CanSeeSectionAsync(Section section, string module, CancellationToken ct = default)
    {
        if (await IsAdminAsync(module, ct) || await IsViewerAsync(ct)) return true;
        if (await MyStaffIdAsync(ct) is not int me) return false;
        return section.ClassTeacherStaffId == me
               || await db.TeachingAssignments.AnyAsync(a => a.StaffId == me && a.SectionId == section.Id && a.EndedOn == null, ct);
    }

    /// <summary>Office roles that view results campus-wide but never enter marks (no marks:Edit).</summary>
    public async Task<bool> IsViewerAsync(CancellationToken ct = default) =>
        !await CanAsync(AppModules.Marks, AppActions.Edit, ct) && await CanAsync(AppModules.Results, AppActions.View, ct);
}
