namespace SchoolManagementSystem.Domain.DTOs.Org;

public record CampusDto(
    int Id,
    string Code,
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string? Website,
    string? InChargeName,
    string? InChargePhone,
    string? InChargeEmail,
    string? InChargeDesignation,
    DateOnly? EstablishedOn,
    int? StudentCapacity,
    string? Notes,
    bool IsActive,
    int AdminUserCount,
    IReadOnlyList<CampusAdminRefDto> Admins);

public record CampusAdminRefDto(int Id, string FullName, string Email);

/// <summary>Optional first Campus Admin created together with a new campus.</summary>
public record NewCampusAdminRequest(string FirstName, string LastName, string Email, string Password);

public record SaveCampusRequest(
    string Code,
    string Name,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    string? Phone,
    string? Email,
    string? Website,
    string? InChargeName,
    string? InChargePhone,
    string? InChargeEmail,
    string? InChargeDesignation,
    DateOnly? EstablishedOn,
    int? StudentCapacity,
    string? Notes,
    bool IsActive = true,
    NewCampusAdminRequest? Admin = null);
