using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;

namespace SchoolManagementSystem.Application.Services.Students;

/// <summary>
/// Guardians are school-wide, but a campus user only sees guardians linked to students they can see.
/// The lookup is the single exception: an exact CNIC/phone match returning only name and child count.
/// </summary>
public class GuardianService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IGuardianService
{
    public async Task<Result<PagedResult<GuardianListItemDto>>> SearchAsync(string? q, int page, int pageSize, CancellationToken ct = default) =>
        await repositories.Guardians.SearchVisibleAsync(q, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public async Task<Result<GuardianDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var guardian = await repositories.Guardians.GetTrackedAsync(id, ct);
        if (guardian == null || !await repositories.Guardians.IsVisibleAsync(id, ct)) return Error.NotFound("Guardian");

        var children = (await repositories.Guardians.GetChildrenAsync(id, ct))
            .Select(x => currentUser.CanAccessCampus(x.CampusId)
                ? x.Child
                : x.Child with { StudentId = null, ClassName = null, SectionName = null })
            .ToList();
        return new GuardianDetailDto(guardian.Id, guardian.FullName, guardian.Cnic, guardian.Phone, guardian.AltPhone,
            guardian.Email, guardian.Occupation, guardian.Address, children);
    }

    public async Task<Result<GuardianDetailDto>> UpdateAsync(int id, SaveGuardianRequest request, CancellationToken ct = default)
    {
        var guardian = await repositories.Guardians.GetTrackedAsync(id, ct);
        if (guardian == null || !await repositories.Guardians.IsVisibleAsync(id, ct)) return Error.NotFound("Guardian");

        if (string.IsNullOrWhiteSpace(request.FullName)) return Error.Validation("Guardian name is required.");
        if (PhoneNormalizer.Digits(request.Phone).Length < 7) return Error.Validation("Guardian phone is required.");
        var cnic = PhoneNormalizer.Digits(request.Cnic);
        if (cnic.Length > 0)
        {
            var other = await repositories.Guardians.FindByCnicAsync(cnic, ct);
            if (other != null && other.Id != id) return Error.Conflict("Another guardian already has this CNIC.");
        }

        StudentService.ApplyGuardian(guardian, request);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<IReadOnlyList<GuardianLookupDto>>> LookupAsync(string? cnic, string? phone, CancellationToken ct = default)
    {
        var cnicDigits = PhoneNormalizer.Digits(cnic);
        var phoneDigits = PhoneNormalizer.Digits(phone);
        if (cnicDigits.Length == 0 && phoneDigits.Length == 0) return Error.Validation("Give a CNIC or a phone number.");
        if (cnicDigits.Length is > 0 and < 13 || phoneDigits.Length is > 0 and < 10)
            return new List<GuardianLookupDto>(); // only full numbers match, so partial input reveals nothing

        return await repositories.Guardians.LookupAsync(cnicDigits, phoneDigits, ct);
    }
}
