namespace SchoolManagementSystem.Domain.DTOs.Dashboard;

public record KpiCardDto(string Id, string Label, string Value, string? Hint, string Tone, string? Href);

public record DashboardDto(IReadOnlyList<KpiCardDto> Kpis);
