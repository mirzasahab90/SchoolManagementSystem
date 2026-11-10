using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Dashboard;

namespace SchoolManagementSystem.Application.Services.Dashboard;

/// <summary>Counts follow the working campus; HQ on "All campuses" sees the consolidated numbers.</summary>
public class DashboardService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker,
    IAttendanceReportService attendanceReports) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var scope = currentUser.DataCampusIds;
        var school = await repositories.Schools.GetDtoAsync(ct);
        var campuses = await repositories.Campuses.CountAsync(scope, ct);
        var activeCampuses = await repositories.Campuses.CountActiveAsync(scope, ct);
        var users = await repositories.Users.CountAsync(scope, ct);
        var activeUsers = await repositories.Users.CountActiveAsync(scope, ct);
        var roles = await repositories.Roles.CountAsync(ct);

        string? activeCampusName = null;
        if (currentUser.ActiveCampusId is int campusId)
        {
            activeCampusName = (await repositories.Campuses.GetDtoAsync(campusId, ct))?.Name;
        }

        var current = await repositories.Sessions.GetEffectiveCurrentAsync(currentUser.ActiveCampusId, ct);
        var sectionCount = current == null ? 0 : await repositories.Sections.CountAsync(current.Id, ct);

        var activeStudents = await repositories.Students.CountActiveAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var admittedThisMonth = await repositories.Students.CountAdmittedSinceAsync(new DateOnly(today.Year, today.Month, 1), ct);

        var scopeLabel = activeCampusName ?? (currentUser.IsHq ? "All campuses" : "No campus selected");
        var kpis = new List<KpiCardDto>
        {
            new("campuses", "Campuses", campuses.ToString(), $"{activeCampuses} active", "primary", "/admin/campuses"),
            new("academics", "Current session", current?.Name ?? "Not set", $"{sectionCount} sections", "primary", "/admin/academics/sections"),
            new("students", "Students", activeStudents.ToString(), $"{admittedThisMonth} admitted this month", "primary", "/admin/students"),
            new("users", "Users", users.ToString(), $"{activeUsers} active logins", "neutral", "/admin/users"),
            new("roles", "Roles", roles.ToString(), "Permission templates", "neutral", "/admin/roles"),
            new("hq", "Access mode", currentUser.IsHq ? "Main Campus" : "Campus", scopeLabel, "primary", null),
        };

        async Task<bool> Can(string module, string action) =>
            currentUser.UserId is int uid && await permissionChecker.HasPermissionAsync(uid, module, action, ct);

        if (await Can(AppModules.Vacancies, AppActions.View))
        {
            // Monday to Sunday (UTC) of the current week.
            var monday = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7));
            var interviews = await repositories.Vacancies.CountInterviewsBetweenAsync(monday, monday.AddDays(7), ct);
            var open = await repositories.Vacancies.CountOpenAsync(ct);
            kpis.Insert(3, new("hiring", "Open vacancies", open.ToString(),
                $"{interviews} {(interviews == 1 ? "interview" : "interviews")} this week", "primary", "/admin/hiring/candidates"));
        }

        if (await Can(AppModules.Attendance, AppActions.View))
        {
            string value = "\u2014";
            var notMarked = 0;
            if (currentUser.ActiveCampusId != null && (await attendanceReports.TodayAsync(ct)) is { IsSuccess: true } att)
            {
                value = att.Value!.PresentPct is decimal pct ? $"{pct:0.#}%" : "\u2014";
                notMarked = att.Value.IsWorkingDay ? att.Value.Sections.Count(s => !s.Marked) : 0;
            }

            kpis.Insert(Math.Min(4, kpis.Count), new("attendance", "Attendance today", value,
                $"{notMarked} {(notMarked == 1 ? "section" : "sections")} not marked", "success", "/admin/attendance/reports"));
        }

        if (await Can(AppModules.Exams, AppActions.View))
        {
            var (examName, submitted, totalPapers) = await repositories.Results.OpenExamProgressAsync(ct);
            var at = kpis.FindIndex(k => k.Id == "attendance");
            kpis.Insert(at >= 0 ? at + 1 : Math.Min(4, kpis.Count), new("exams", "Exams", examName ?? "—",
                $"{submitted}/{totalPapers} sections submitted", "primary", "/admin/exams"));
        }

        var quickActions = new List<QuickActionDto>();
        if (currentUser.IsHq && await Can(AppModules.Campuses, AppActions.Add))
        {
            quickActions.Add(new("add-campus", "Add campus", "Register a new branch with location and in-charge", "/admin/campuses"));
        }

        if (currentUser.ActiveCampusId != null && await Can(AppModules.Students, AppActions.Add))
        {
            quickActions.Add(new("admit", "New admission", "Admit a student with guardians and a section", "/admin/students/new"));
        }

        if (await Can(AppModules.Users, AppActions.Add))
        {
            quickActions.Add(new("add-user", "Add user", "Create a login and assign campus access", "/admin/users"));
        }

        if (await Can(AppModules.Roles, AppActions.View))
        {
            quickActions.Add(new("roles", "Role permissions", "Set View / Add / Edit / Delete per menu", "/admin/roles"));
        }

        return new DashboardDto(
            school?.Name ?? "School Management System",
            school?.ShortName,
            activeCampusName,
            kpis,
            quickActions);
    }
}
