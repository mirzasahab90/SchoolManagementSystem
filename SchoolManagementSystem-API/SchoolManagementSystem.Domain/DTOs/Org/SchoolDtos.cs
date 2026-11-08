namespace SchoolManagementSystem.Domain.DTOs.Org;

public record SchoolDto(int Id, string Name, string? ShortName, string? Phone, string? Email, string? Address);

public record SaveSchoolRequest(string Name, string? ShortName, string? Phone, string? Email, string? Address);
