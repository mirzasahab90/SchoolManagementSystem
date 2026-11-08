namespace SchoolManagementSystem.Domain.DTOs.Dashboard;

public record KpiCardDto(string Id, string Label, string Value, string? Hint, string Tone, string? Href);

public record QuickActionDto(string Id, string Label, string Description, string Href);

public record DashboardDto(
    string SchoolName,
    string? SchoolShortName,
    string? ActiveCampusName,
    IReadOnlyList<KpiCardDto> Kpis,
    IReadOnlyList<QuickActionDto> QuickActions);
