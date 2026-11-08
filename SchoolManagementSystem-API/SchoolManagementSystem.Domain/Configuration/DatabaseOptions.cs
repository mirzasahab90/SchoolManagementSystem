namespace SchoolManagementSystem.Domain.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations when the API starts. You only add migrations; update runs here.</summary>
    public bool RunMigrationsOnStartup { get; set; } = true;

    /// <summary>Insert missing seed data (roles, menus, SuperAdmin, demo campuses) on start.</summary>
    public bool RunSeederOnStartup { get; set; } = true;
}
