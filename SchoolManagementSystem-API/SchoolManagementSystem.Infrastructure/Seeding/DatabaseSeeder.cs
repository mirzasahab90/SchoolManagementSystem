using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Infrastructure.Seeding;

/// <summary>Idempotent seed of roles, permissions, menus, demo campuses, and SuperAdmin.</summary>
public class DatabaseSeeder(
    SchoolManagementSystemDbContext db,
    IPasswordHasher<User> passwordHasher,
    ILogger<DatabaseSeeder> logger)
{
    public const string AdminEmail = "admin@sms.com";
    public const string AdminPassword = "@123@123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedAndSyncModulesAsync(ct);
        await SeedRolesAsync(ct);
        await SeedSchoolAndCampusesAsync(ct);
        await SeedSuperAdminAsync(ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Database seed completed.");
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var order = existing.Count + 1;
        foreach (var code in AppActions.All)
        {
            if (existing.Contains(code)) continue;
            db.Permissions.Add(new Permission { Code = code, Name = code, DisplayOrder = order++ });
        }
    }

    /// <summary>
    /// HQ menu: Dashboard, Campuses, Settings (Users + Roles children). Upserts so older seeds are reshaped.
    /// </summary>
    private async Task SeedAndSyncModulesAsync(CancellationToken ct)
    {
        async Task<Module> Upsert(string key, string name, string? route, string? icon, int order, int? parentId)
        {
            var module = await db.Modules.FirstOrDefaultAsync(m => m.Key == key, ct);
            if (module == null)
            {
                module = new Module { Key = key };
                db.Modules.Add(module);
            }

            module.Name = name;
            module.Route = route;
            module.Icon = icon;
            module.DisplayOrder = order;
            module.ParentId = parentId;
            module.IsActive = true;
            return module;
        }

        var dashboard = await Upsert(AppModules.Dashboard, "Dashboard", "/admin", "layout-dashboard", 1, null);
        var campuses = await Upsert(AppModules.Campuses, "Campuses", "/admin/campuses", "building-2", 2, null);
        var settings = await Upsert(AppModules.Settings, "Settings", null, "settings", 3, null);
        await db.SaveChangesAsync(ct);

        await Upsert(AppModules.Users, "Users", "/admin/users", "users", 1, settings.Id);
        await Upsert(AppModules.Roles, "Roles", "/admin/roles", "shield", 2, settings.Id);
        await db.SaveChangesAsync(ct);

        _ = dashboard;
        _ = campuses;
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        if (await db.Roles.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        db.Roles.AddRange(
            new Role { Name = SystemRoles.SuperAdmin, Description = "Full HQ access", IsSystem = true, IsActive = true },
            new Role { Name = SystemRoles.Principal, Description = "Campus principal / admin", IsSystem = true, IsActive = true },
            new Role { Name = SystemRoles.Accountant, Description = "Fees and accounts", IsSystem = true, IsActive = true },
            new Role { Name = SystemRoles.Teacher, Description = "Teaching staff", IsSystem = true, IsActive = true },
            new Role { Name = SystemRoles.Clerk, Description = "Front desk / admissions", IsSystem = true, IsActive = true },
            new Role { Name = SystemRoles.Parent, Description = "Parent portal", IsSystem = true, IsActive = true },
            new Role { Name = SystemRoles.Student, Description = "Student portal", IsSystem = true, IsActive = true });
    }

    private async Task SeedSchoolAndCampusesAsync(CancellationToken ct)
    {
        if (await db.Schools.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var school = new School
        {
            Name = "Demo School",
            ShortName = "DEMO",
            Email = "info@demoschool.local",
            Address = "Main City"
        };
        db.Schools.Add(school);

        db.Campuses.AddRange(
            new Campus
            {
                School = school,
                Code = "MAIN",
                Name = "Main Campus",
                Address = "Main Campus Road",
                City = "Karachi",
                Country = "Pakistan",
                InChargeName = "Campus Head",
                InChargeDesignation = "Principal",
                IsActive = true
            },
            new Campus
            {
                School = school,
                Code = "EAST",
                Name = "East Campus",
                Address = "East Side Avenue",
                City = "Karachi",
                Country = "Pakistan",
                InChargeName = "East Head",
                InChargeDesignation = "Vice Principal",
                IsActive = true
            });
    }

    private async Task SeedSuperAdminAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);

        var role = await db.Roles.FirstAsync(r => r.Name == SystemRoles.SuperAdmin, ct);
        var campuses = await db.Campuses.ToListAsync(ct);

        var admin = await db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .Include(u => u.UserCampuses)
            .FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);

        if (admin == null)
        {
            admin = new User
            {
                FirstName = "System",
                LastName = "Admin",
                Email = AdminEmail,
                IsActive = true,
                IsHq = true,
                MustChangePassword = false
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, AdminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            admin.IsActive = true;
            admin.IsHq = true;
            admin.IsDeleted = false;
            if (string.IsNullOrEmpty(admin.PasswordHash))
            {
                admin.PasswordHash = passwordHasher.HashPassword(admin, AdminPassword);
            }
        }

        if (!admin.UserRoles.Any(ur => ur.RoleId == role.Id))
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = admin.Id,
                RoleId = role.Id,
                AssignedOn = DateTime.UtcNow
            });
        }

        if (admin.UserCampuses.Count == 0)
        {
            var primary = true;
            foreach (var campus in campuses)
            {
                db.UserCampuses.Add(new UserCampus
                {
                    UserId = admin.Id,
                    CampusId = campus.Id,
                    IsPrimary = primary
                });
                primary = false;
            }
        }

        var modules = await db.Modules.ToListAsync(ct);
        var permissions = await db.Permissions.ToListAsync(ct);
        var existingRights = await db.RoleModulePermissions
            .Where(rmp => rmp.RoleId == role.Id)
            .Select(rmp => new { rmp.ModuleId, rmp.PermissionId })
            .ToListAsync(ct);
        var existingSet = existingRights.Select(x => (x.ModuleId, x.PermissionId)).ToHashSet();

        foreach (var module in modules)
        {
            foreach (var permission in permissions)
            {
                if (existingSet.Contains((module.Id, permission.Id))) continue;
                db.RoleModulePermissions.Add(new RoleModulePermission
                {
                    RoleId = role.Id,
                    ModuleId = module.Id,
                    PermissionId = permission.Id
                });
            }
        }
    }
}
