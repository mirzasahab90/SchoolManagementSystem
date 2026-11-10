using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Application.Services.Students;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>
/// Staff records: search, profile, direct onboarding, edit and delete. The other partial files hold files,
/// checklist, login account, teaching assignments and the status lifecycle. Staff is campus data
/// (filtered and guarded by the DbContext).
/// </summary>
public partial class StaffService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    IFileStorage storage,
    IUserService userService) : IStaffService
{
    private static readonly Error CnicTaken = Error.Conflict("This CNIC belongs to an active staff member.");

    private static readonly Dictionary<StaffStatus, StaffStatus[]> StatusMoves = new()
    {
        [StaffStatus.Onboarding] = [StaffStatus.Resigned, StaffStatus.Terminated],
        [StaffStatus.Active] = [StaffStatus.OnLeave, StaffStatus.Resigned, StaffStatus.Terminated],
        [StaffStatus.OnLeave] = [StaffStatus.Active, StaffStatus.Resigned, StaffStatus.Terminated],
        [StaffStatus.Resigned] = [],
        [StaffStatus.Terminated] = []
    };

    public async Task<Result<PagedResult<StaffListItemDto>>> SearchAsync(
        string? q, StaffStatus? status, StaffCategory? category, StaffDesignation? designation, string? sort, string? dir,
        int page, int pageSize, CancellationToken ct = default) =>
        await repositories.Staff.SearchAsync(q, status, category, designation, sort, QueryOrdering.IsDescending(dir),
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public async Task<Result<IReadOnlyList<StaffDesignation>>> DesignationsAsync(CancellationToken ct = default) =>
        await repositories.Staff.DesignationsAsync(ct);

    public async Task<Result<StaffDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await repositories.Staff.GetDetailAsync(id, ct);
        return dto == null ? Error.NotFound("Staff member") : dto;
    }

    public async Task<Result<StaffProfileDto>> GetProfileAsync(int id, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");

        var detail = (await repositories.Staff.GetDetailAsync(id, ct))!;
        var personal = new StaffPersonalDto(
            staff.Gender, staff.DateOfBirth, staff.Address, staff.PhotoPath != null, staff.EmergencyName, staff.EmergencyRelation,
            staff.EmergencyPhone, staff.Qualification, staff.ExperienceYears, staff.PreviousEmployer, staff.BankName,
            Mask(staff.BankAccount), staff.LeftOn, staff.LeaveReason);
        var documents = staff.Documents
            .OrderByDescending(d => d.UploadedOn)
            .Select(d => new StaffDocumentDto(d.Id, d.Type, d.FileName, d.ContentType, d.SizeBytes, d.UploadedOn))
            .ToList();
        var account = staff.UserId is int uid ? await repositories.Staff.GetAccountAsync(uid, ct) : null;

        return new StaffProfileDto(
            detail, personal, BuildChecklist(staff), documents,
            await repositories.Staff.GetCurrentAssignmentsAsync(id, await CurrentSessionIdAsync(staff.CampusId, ct), ct),
            await repositories.Staff.GetClassTeacherOfAsync(id, ct),
            account,
            StatusMoves[staff.Status]);
    }

    public async Task<Result<StaffBankDto>> GetBankAsync(int id, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        return new StaffBankDto(staff.BankName, staff.BankAccount);
    }

    public async Task<Result<StaffProfileDto>> CreateAsync(SaveStaffRequest request, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var error = Validate(request);
        if (error != null) return error;

        var cnic = CnicDigits(request.Cnic);
        if (cnic != null && await repositories.Staff.CnicActiveAsync(cnic, ct)) return CnicTaken;

        var staff = new Staff
        {
            CampusId = campusId,
            EmployeeNo = await NextEmployeeNoAsync(repositories, campusId, ct),
            Status = StaffStatus.Onboarding
        };
        Apply(staff, request, cnic, isNew: true);
        repositories.Staff.Add(staff);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(staff.Id, ct);
    }

    public async Task<Result<StaffProfileDto>> UpdateAsync(int id, SaveStaffRequest request, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        var error = Validate(request);
        if (error != null) return error;

        var cnic = CnicDigits(request.Cnic);
        if (cnic != null && cnic != staff.Cnic && IsCurrent(staff.Status) && await repositories.Staff.CnicActiveAsync(cnic, ct, id))
            return CnicTaken;
        if (request.Category == StaffCategory.NonTeaching && staff.Category == StaffCategory.Teaching)
        {
            var session = await CurrentSessionIdAsync(staff.CampusId, ct);
            if (staff.Assignments.Any(a => a.EndedOn == null && a.AcademicSessionId == session))
                return Error.Validation("End this member's teaching assignments before changing them to non-teaching.");
        }

        Apply(staff, request, cnic, isNew: false);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        if (staff.SourceCandidateId != null || staff.Status != StaffStatus.Onboarding || staff.UserId != null
            || await repositories.Staff.HasAnyAssignmentAsync(id, ct)
            || (await repositories.Teaching.ClassTeacherSectionsAsync(id, ct)).Count > 0)
        {
            return Error.Validation("Only staff added directly, still onboarding, without a login or classes can be deleted. Use Resign instead.");
        }

        foreach (var doc in staff.Documents) repositories.Staff.Remove(doc);
        repositories.Staff.Remove(staff);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Staff member deleted.");
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Next free {CampusCode}-EMP-{0000}; shared with hiring.</summary>
    internal static async Task<string> NextEmployeeNoAsync(SchoolManagementSystemRepositories repositories, int campusId, CancellationToken ct)
    {
        var code = (await repositories.Campuses.GetDtoAsync(campusId, ct))!.Code;
        string employeeNo;
        do
        {
            var n = await repositories.Sequences.NextAsync(campusId, "EMP", ct);
            employeeNo = $"{code}-EMP-{n:0000}";
        } while (await repositories.Staff.EmployeeNoTakenAsync(campusId, employeeNo, ct));
        return employeeNo;
    }

    private async Task<int?> CurrentSessionIdAsync(int campusId, CancellationToken ct) =>
        (await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct))?.Id;

    private static bool IsCurrent(StaffStatus status) => status is not (StaffStatus.Resigned or StaffStatus.Terminated);

    internal static string? Mask(string? account) =>
        string.IsNullOrEmpty(account) ? null : "****" + account[^Math.Min(4, account.Length)..];

    private static string? CnicDigits(string? value)
    {
        var digits = PhoneNormalizer.Digits(value);
        return digits.Length == 0 ? null : digits;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error? Validate(SaveStaffRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName)) return Error.Validation("First and last name are required.");
        if (r.FirstName.Trim().Length > 100 || r.LastName.Trim().Length > 100) return Error.Validation("Names are limited to 100 characters.");
        if (string.IsNullOrWhiteSpace(r.Phone)) return Error.Validation("Phone is required.");
        if (r.Phone.Trim().Length > 50 || r.EmergencyPhone?.Trim().Length > 50) return Error.Validation("Phone is too long.");
        var cnic = CnicDigits(r.Cnic);
        if (cnic != null && cnic.Length != 13) return Error.Validation("CNIC must have 13 digits.");
        if (r.Email?.Trim().Length > 256) return Error.Validation("Email is too long.");
        if (r.Address?.Trim().Length > 300) return Error.Validation("Address is too long (300 characters at most).");
        if (r.EmergencyName?.Trim().Length > 150 || r.EmergencyRelation?.Trim().Length > 50) return Error.Validation("Emergency contact is too long.");
        if (r.Qualification?.Trim().Length > 200 || r.PreviousEmployer?.Trim().Length > 200) return Error.Validation("Qualification and previous employer are limited to 200 characters.");
        if (r.BankName?.Trim().Length > 100) return Error.Validation("Bank name is too long.");
        if (!string.IsNullOrEmpty(r.BankAccount))
        {
            var account = r.BankAccount.Replace(" ", "");
            if (account.Length > 34 || !account.All(char.IsLetterOrDigit)) return Error.Validation("Bank account must be letters and digits, 34 at most.");
        }

        if (r.ExperienceYears is < 0 or > 99) return Error.Validation("Experience must be between 0 and 99 years.");
        if (r.BasicSalary <= 0 || r.BasicSalary > 9_999_999_999m) return Error.Validation("The salary must be greater than 0.");
        if (!Enum.IsDefined(r.Category) || !Enum.IsDefined(r.Designation) || !Enum.IsDefined(r.EmploymentType))
            return Error.Validation("Pick a category, designation and employment type.");
        if (r.Gender is { } g && !Enum.IsDefined(g)) return Error.Validation("Unknown gender.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (r.DateOfBirth is { } dob && (dob > today.AddYears(-16) || dob < today.AddYears(-80)))
            return Error.Validation("Date of birth must give an age between 16 and 80.");
        if (r.JoiningDate > today.AddYears(1)) return Error.Validation("The joining date is too far ahead.");
        return null;
    }

    private static void Apply(Staff s, SaveStaffRequest r, string? cnic, bool isNew)
    {
        s.FirstName = r.FirstName.Trim();
        s.LastName = r.LastName.Trim();
        s.Cnic = cnic;
        s.Phone = r.Phone.Trim();
        s.Email = Trim(r.Email);
        s.Gender = r.Gender;
        s.DateOfBirth = r.DateOfBirth;
        s.Address = Trim(r.Address);
        s.EmergencyName = Trim(r.EmergencyName);
        s.EmergencyRelation = Trim(r.EmergencyRelation);
        s.EmergencyPhone = Trim(r.EmergencyPhone);
        s.Category = r.Category;
        s.Designation = r.Designation;
        s.EmploymentType = r.EmploymentType;
        s.JoiningDate = r.JoiningDate;
        s.BasicSalary = r.BasicSalary;
        s.Qualification = Trim(r.Qualification);
        s.ExperienceYears = r.ExperienceYears;
        s.PreviousEmployer = Trim(r.PreviousEmployer);
        s.BankName = Trim(r.BankName);
        if (isNew || r.BankAccount != null)
        {
            s.BankAccount = string.IsNullOrWhiteSpace(r.BankAccount) ? null : r.BankAccount.Replace(" ", "").ToUpperInvariant();
        }
    }
}
