using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Infrastructure.Seeding;

/// <summary>
/// Ensures the CampusAdmin role exists and gives the system roles sensible starting permissions,
/// so no newly created user lands on an empty sidebar. Defaults are only written to roles that have
/// no permissions yet, so HQ's later edits are never overwritten. CampusAdmin is additionally topped up
/// with every campus-level module on each run, so future modules reach campus admins automatically.
/// </summary>
public static class RoleDefaultsSeeder
{
    private const string V = AppActions.View, A = AppActions.Add, E = AppActions.Edit, D = AppActions.Delete;
    private static readonly string[] Full = [V, A, E, D];

    private static readonly Dictionary<string, Dictionary<string, string[]>> Defaults = new()
    {
        [SystemRoles.CampusAdmin] = new()
        {
            [AppModules.Campuses] = [V, E],
        },
        [SystemRoles.Principal] = new()
        {
            [AppModules.Dashboard] = Full,
            [AppModules.MyCampus] = Full,
            [AppModules.Campuses] = [V, E],
            [AppModules.Users] = Full,
            [AppModules.Roles] = [V],
            [AppModules.AcademicSessions] = Full,
            [AppModules.Classes] = Full,
            [AppModules.Subjects] = Full,
            [AppModules.Sections] = Full,
            [AppModules.Students] = Full,
            [AppModules.Guardians] = Full,
            [AppModules.Vacancies] = Full,
            [AppModules.Candidates] = Full,
            [AppModules.Staff] = Full,
            [AppModules.Attendance] = Full,
            [AppModules.AttendanceReports] = [V],
            [AppModules.StaffAttendance] = Full,
            [AppModules.Calendar] = Full,
            [AppModules.Exams] = Full,
            [AppModules.Marks] = Full,
            [AppModules.Results] = Full,
            [AppModules.Grading] = Full,
            [AppModules.FeeSetup] = Full,
            [AppModules.FeeVouchers] = Full,
            [AppModules.FeeCollection] = Full,
            [AppModules.FeeReports] = Full,
        },
        [SystemRoles.Teacher] = new()
        {
            [AppModules.Dashboard] = [V],
            [AppModules.Sections] = [V],
            [AppModules.Classes] = [V],
            [AppModules.Subjects] = [V],
            [AppModules.Students] = [V],
            [AppModules.Attendance] = [V, A, E],
            [AppModules.AttendanceReports] = [V],
            [AppModules.Exams] = [V],
            [AppModules.Marks] = [V, E],
            [AppModules.Results] = [V],
        },
        [SystemRoles.Clerk] = new()
        {
            [AppModules.Dashboard] = [V],
            [AppModules.Sections] = [V],
            [AppModules.Users] = [V, A],
            [AppModules.Students] = [V, A],
            [AppModules.Guardians] = [V, E],
            [AppModules.Attendance] = [V],
            [AppModules.AttendanceReports] = [V],
            [AppModules.StaffAttendance] = [V, A, E],
            [AppModules.Calendar] = [V],
            [AppModules.Exams] = [V],
            [AppModules.Results] = [V],
            [AppModules.FeeVouchers] = [V],
            [AppModules.FeeCollection] = [V, A],
        },
        [SystemRoles.Accountant] = new()
        {
            [AppModules.Dashboard] = [V],
            [AppModules.FeeSetup] = Full,
            [AppModules.FeeVouchers] = Full,
            [AppModules.FeeCollection] = Full,
            [AppModules.FeeReports] = Full,
        },
    };

    /// <summary>
    /// When a module is created by this seed run, give the HQ system roles their default grants for it.
    /// Later runs see the module already exists and add nothing, so HQ's edits are never overwritten.
    /// </summary>
    public static async Task GrantNewModulesAsync(SchoolManagementSystemDbContext db, IReadOnlySet<string> createdKeys, CancellationToken ct)
    {
        if (createdKeys.Count == 0) return;

        var roles = await db.Roles.IgnoreQueryFilters().Where(r => !r.IsDeleted && r.CampusId == null && r.IsSystem).ToListAsync(ct);
        var modules = await db.Modules.Where(m => createdKeys.Contains(m.Key)).ToDictionaryAsync(m => m.Key, StringComparer.OrdinalIgnoreCase, ct);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, ct);
        var roleIds = roles.Select(r => r.Id).ToList();
        var existing = (await db.RoleModulePermissions.Where(x => roleIds.Contains(x.RoleId))
                .Select(x => new { x.RoleId, x.ModuleId, x.PermissionId }).ToListAsync(ct))
            .Select(x => (x.RoleId, x.ModuleId, x.PermissionId)).ToHashSet();

        foreach (var role in roles)
        {
            if (!Defaults.TryGetValue(role.Name, out var grants)) continue;
            foreach (var (moduleKey, actions) in grants)
            {
                if (!modules.TryGetValue(moduleKey, out var module)) continue;
                foreach (var action in actions)
                {
                    if (!permissions.TryGetValue(action, out var permission)) continue;
                    if (!existing.Add((role.Id, module.Id, permission.Id))) continue;
                    db.RoleModulePermissions.Add(new RoleModulePermission { RoleId = role.Id, ModuleId = module.Id, PermissionId = permission.Id });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task SeedAsync(SchoolManagementSystemDbContext db, CancellationToken ct)
    {
        var roles = await db.Roles.IgnoreQueryFilters().Where(r => !r.IsDeleted && r.CampusId == null).ToListAsync(ct);

        var campusAdmin = roles.FirstOrDefault(r => r.Name == SystemRoles.CampusAdmin);
        if (campusAdmin == null)
        {
            campusAdmin = new Role
            {
                Name = SystemRoles.CampusAdmin,
                Description = "Runs one campus",
                IsSystem = true,
                IsActive = true,
                HqAssignOnly = true
            };
            db.Roles.Add(campusAdmin);
            roles.Add(campusAdmin);
        }

        foreach (var role in roles.Where(r => r.Name is SystemRoles.SuperAdmin or SystemRoles.CampusAdmin))
        {
            role.HqAssignOnly = true;
        }

        await db.SaveChangesAsync(ct);

        var modules = await db.Modules.ToDictionaryAsync(m => m.Key, StringComparer.OrdinalIgnoreCase, ct);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, ct);
        var roleIds = roles.Select(r => r.Id).ToList();
        var existing = (await db.RoleModulePermissions
                .Where(x => roleIds.Contains(x.RoleId))
                .Select(x => new { x.RoleId, x.ModuleId, x.PermissionId })
                .ToListAsync(ct))
            .Select(x => (x.RoleId, x.ModuleId, x.PermissionId))
            .ToHashSet();

        void Grant(Role role, string moduleKey, IEnumerable<string> actions)
        {
            if (!modules.TryGetValue(moduleKey, out var module)) return;
            foreach (var action in actions)
            {
                if (!permissions.TryGetValue(action, out var permission)) continue;
                if (!existing.Add((role.Id, module.Id, permission.Id))) continue;
                db.RoleModulePermissions.Add(new RoleModulePermission
                {
                    RoleId = role.Id,
                    ModuleId = module.Id,
                    PermissionId = permission.Id
                });
            }
        }

        foreach (var role in roles)
        {
            if (!Defaults.TryGetValue(role.Name, out var grants)) continue;
            var hasAny = existing.Any(x => x.RoleId == role.Id);
            if (hasAny && role.Name != SystemRoles.CampusAdmin) continue;
            if (!hasAny)
            {
                foreach (var (moduleKey, actions) in grants)
                {
                    Grant(role, moduleKey, actions);
                }
            }
        }

        // CampusAdmin: full rights on every campus-level module, topped up on every run.
        foreach (var module in modules.Values.Where(m => m.CampusLevel && m.IsActive))
        {
            Grant(campusAdmin, module.Key, Full);
        }

        await db.SaveChangesAsync(ct);
    }
}
