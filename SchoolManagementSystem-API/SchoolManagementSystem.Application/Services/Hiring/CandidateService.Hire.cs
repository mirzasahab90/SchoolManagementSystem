using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>Hire: candidate → basic staff record, in one transaction.</summary>
public partial class CandidateService
{
    public async Task<Result<StaffDetailDto>> HireAsync(int id, HireRequest request, CancellationToken ct = default)
    {
        if (request.BasicSalary <= 0 || request.BasicSalary > 9_999_999_999m) return Error.Validation("The salary must be greater than 0.");
        if (!Enum.IsDefined(request.EmploymentType)) return Error.Validation("Pick an employment type.");

        await using var tx = await repositories.BeginTransactionAsync(ct);

        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        if (candidate.Stage == CandidateStage.Hired || candidate.StaffId != null) return Error.Conflict("This candidate is already hired.");

        var offer = LatestOffer(candidate);
        if (candidate.Stage != CandidateStage.Offered || offer?.Status != OfferStatus.Accepted)
            return Error.Validation("The candidate must accept an offer before hiring.");
        if (candidate.Cnic != null && await repositories.Staff.CnicActiveAsync(candidate.Cnic, ct))
            return Error.Conflict("This CNIC belongs to an active staff member.");

        // Taking the number locks the campus's EMP sequence row until commit, so hires on a campus run one at a
        // time from here: the positions count below cannot be raced. A refusal rolls the number back.
        var employeeNo = await StaffService.NextEmployeeNoAsync(repositories, candidate.CampusId, ct);
        var hiredSoFar = await repositories.Vacancies.CountHiredAsync(candidate.VacancyId, ct);
        if (hiredSoFar >= candidate.Vacancy.Positions) return Error.Validation("All positions are filled. Raise the positions first.");
        if (candidate.Vacancy.Status != VacancyStatus.Open) return Error.Validation("This vacancy is not open.");

        var parts = candidate.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var staff = new Staff
        {
            CampusId = candidate.CampusId,
            EmployeeNo = employeeNo,
            FirstName = parts.Length > 1 ? string.Join(' ', parts[..^1]) : parts[0],
            LastName = parts.Length > 1 ? parts[^1] : "-",
            Cnic = candidate.Cnic,
            Phone = candidate.Phone,
            Email = candidate.Email,
            Category = candidate.Vacancy.Category,
            Designation = offer.Designation,
            EmploymentType = request.EmploymentType,
            JoiningDate = request.JoiningDate,
            BasicSalary = request.BasicSalary,
            Status = StaffStatus.Onboarding,
            SourceCandidateId = candidate.Id,
            Qualification = candidate.Qualification,
            ExperienceYears = candidate.ExperienceYears
        };
        repositories.Staff.Add(staff);
        await repositories.SaveChangesAsync(ct);

        candidate.StaffId = staff.Id;
        SetStage(candidate, CandidateStage.Hired, null);

        if (hiredSoFar + 1 >= candidate.Vacancy.Positions) candidate.Vacancy.Status = VacancyStatus.Filled;

        await repositories.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await repositories.Staff.GetDetailAsync(staff.Id, ct))!;
    }
}
