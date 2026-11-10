using Microsoft.AspNetCore.Http;
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
/// Candidates and their pipeline. Candidates, rounds and offers are campus data (filtered and guarded by the
/// DbContext); every stage change writes an append-only history row.
/// </summary>
public partial class CandidateService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    IFileStorage storage) : ICandidateService
{
    public const long CvLimit = 10_485_760;
    private static readonly string[] CvExtensions = [".pdf", ".doc", ".docx", ".jpg", ".png"];
    private static readonly Error VacancyNotOpen = Error.Validation("This vacancy is not open.");
    private static readonly Error ReasonRequired = Error.Validation("A reason is required.");

    public async Task<Result<PagedResult<CandidateListItemDto>>> SearchAsync(
        int? vacancyId, string? stage, string? q, CandidateSource? source, DateOnly? appliedFrom, DateOnly? appliedTo,
        int page, int pageSize, CancellationToken ct = default) =>
        await repositories.Candidates.SearchAsync(vacancyId, stage, q, source, appliedFrom, appliedTo,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public async Task<Result<CandidateProfileDto>> GetProfileAsync(int id, CancellationToken ct = default)
    {
        var c = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (c == null) return Error.NotFound("Candidate");

        string? warning = null;
        if (c.Cnic != null)
        {
            var employeeNo = await repositories.Candidates.ActiveStaffWithCnicAsync(c.Cnic, c.Id, ct);
            if (employeeNo != null) warning = $"Already on staff as {employeeNo}";
        }

        var detail = new CandidateDetailDto(
            c.Id, c.CampusId, c.Campus.Code, c.VacancyId, c.Vacancy.Title, c.FullName, c.Cnic, c.Phone, c.Email,
            c.Qualification, c.ExperienceYears, c.CurrentSalary, c.ExpectedSalary, c.Source, c.Stage, c.StageReason,
            c.CvPath != null, c.CvFileName, c.StaffId, warning);
        var offer = LatestOffer(c);
        return new CandidateProfileDto(
            detail,
            await repositories.Candidates.GetInterviewsAsync(id, ct),
            offer == null ? null : new OfferDto(offer.Id, offer.Designation, offer.EmploymentType, offer.OfferedSalary,
                offer.ProposedJoiningDate, offer.Status, offer.RespondedOn),
            await repositories.Candidates.GetHistoryAsync(id, ct),
            CandidatePipeline.Allowed(c.Stage));
    }

    public async Task<Result<CandidateProfileDto>> CreateAsync(SaveCandidateRequest request, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error != null) return error;

        var vacancy = await repositories.Vacancies.GetTrackedAsync(request.VacancyId, ct);
        if (vacancy == null) return Error.Validation("Pick a vacancy of this campus.");
        if (vacancy.Status != VacancyStatus.Open) return VacancyNotOpen;

        var cnic = CnicDigits(request.Cnic);
        if (cnic != null && await repositories.Candidates.CnicTakenInVacancyAsync(vacancy.Id, cnic, null, ct))
            return Error.Conflict("A candidate with this CNIC already applied for this vacancy.");

        var candidate = new Candidate { CampusId = vacancy.CampusId, VacancyId = vacancy.Id, Stage = CandidateStage.Applied };
        Apply(candidate, request, cnic);
        repositories.Candidates.Add(candidate);
        await repositories.SaveChangesAsync(ct);

        AddHistory(candidate.Id, null, CandidateStage.Applied, null);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(candidate.Id, ct);
    }

    public async Task<Result<CandidateProfileDto>> UpdateAsync(int id, SaveCandidateRequest request, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        var error = Validate(request);
        if (error != null) return error;

        if (request.VacancyId != candidate.VacancyId)
        {
            if (candidate.Stage != CandidateStage.Applied)
                return Error.Validation("The vacancy can only change while the candidate is Applied.");
            var vacancy = await repositories.Vacancies.GetTrackedAsync(request.VacancyId, ct);
            if (vacancy == null || vacancy.CampusId != candidate.CampusId) return Error.Validation("Pick a vacancy of this campus.");
            if (vacancy.Status != VacancyStatus.Open) return VacancyNotOpen;
            candidate.VacancyId = vacancy.Id;
        }

        var cnic = CnicDigits(request.Cnic);
        if (cnic != null && await repositories.Candidates.CnicTakenInVacancyAsync(candidate.VacancyId, cnic, id, ct))
            return Error.Conflict("A candidate with this CNIC already applied for this vacancy.");

        Apply(candidate, request, cnic);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<CandidateProfileDto>> MoveAsync(int id, MoveCandidateRequest request, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");

        var allowed = CandidatePipeline.Allowed(candidate.Stage);
        if (!allowed.Contains(request.Stage))
        {
            return Error.Validation(allowed.Count == 0
                ? "This candidate's stage is final."
                : $"Allowed next stages: {string.Join(", ", allowed)}.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > 500) return Error.Validation("The reason is too long (500 characters at most).");
        var needsReason = request.Stage is CandidateStage.Rejected or CandidateStage.Withdrawn or CandidateStage.Applied;
        if (needsReason && reason == null) return ReasonRequired;
        if (request.Stage == CandidateStage.Applied && candidate.Vacancy.Status != VacancyStatus.Open) return VacancyNotOpen;

        SetStage(candidate, request.Stage, reason);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        if (candidate.Stage is not (CandidateStage.Applied or CandidateStage.Rejected or CandidateStage.Withdrawn))
            return Error.Validation("Only Applied, Rejected or Withdrawn candidates can be deleted. Reject or withdraw first.");

        foreach (var round in candidate.Interviews) repositories.Candidates.Remove(round);
        foreach (var offer in candidate.Offers) repositories.Candidates.Remove(offer);
        repositories.Candidates.Remove(candidate);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Candidate deleted.");
    }

    public async Task<Result> SetCvAsync(int id, IFormFile file, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");

        var ext = await CheckCvAsync(file, ct);
        if (ext == null) return Error.Validation("Only PDF, DOC, DOCX, JPG or PNG files up to 10 MB.");

        var old = candidate.CvPath;
        await using var stream = file.OpenReadStream();
        candidate.CvPath = await storage.SaveAsync($"{candidate.CampusId}/candidates/{candidate.Id}", ext, stream, ct);
        candidate.CvFileName = Path.GetFileName(file.FileName) is { Length: > 0 and <= 255 } name ? name : $"cv{ext}";
        await repositories.SaveChangesAsync(ct);
        if (old != null) await storage.DeleteAsync(old, ct);
        return Result.Ok("CV uploaded.");
    }

    public async Task<Result<(Stream Content, string ContentType, string FileName)>> GetCvAsync(int id, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate?.CvPath == null) return Error.NotFound("CV");
        var stream = await storage.OpenAsync(candidate.CvPath, ct);
        if (stream == null) return Error.NotFound("CV");
        return (stream, FileSignature.ContentType(Path.GetExtension(candidate.CvPath)), candidate.CvFileName ?? "cv");
    }

    // ---------------------------------------------------------------- helpers

    private void SetStage(Candidate candidate, CandidateStage to, string? reason)
    {
        var from = candidate.Stage;
        candidate.Stage = to;
        candidate.StageReason = reason;
        AddHistory(candidate.Id, from, to, reason);
    }

    private void AddHistory(int candidateId, CandidateStage? from, CandidateStage to, string? reason) =>
        repositories.Candidates.AddHistory(new CandidateStageHistory
        {
            CandidateId = candidateId,
            FromStage = from,
            ToStage = to,
            Reason = reason,
            OccurredOn = DateTime.UtcNow,
            ByUserId = currentUser.UserId
        });

    private static Offer? LatestOffer(Candidate c) =>
        c.Offers.OrderByDescending(o => o.CreatedOn).ThenByDescending(o => o.Id).FirstOrDefault();

    private static string? CnicDigits(string? value)
    {
        var digits = PhoneNormalizer.Digits(value);
        return digits.Length == 0 ? null : digits;
    }

    private static Error? Validate(SaveCandidateRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FullName)) return Error.Validation("Name is required.");
        // Staff first / last names hold 100 characters each; refusing here keeps hire from failing later.
        if (r.FullName.Trim().Length > 100) return Error.Validation("Name is too long (100 characters at most).");
        if (string.IsNullOrWhiteSpace(r.Phone)) return Error.Validation("Phone is required.");
        if (r.Phone.Trim().Length > 50) return Error.Validation("Phone is too long.");
        var cnic = CnicDigits(r.Cnic);
        if (cnic != null && cnic.Length != 13) return Error.Validation("CNIC must have 13 digits.");
        if (r.Email?.Trim().Length > 256) return Error.Validation("Email is too long.");
        if (r.Qualification?.Trim().Length > 200) return Error.Validation("Qualification is too long (200 characters at most).");
        if (r.ExperienceYears is < 0 or > 99) return Error.Validation("Experience must be between 0 and 99 years.");
        if (r.CurrentSalary is < 0 or > 9_999_999_999m || r.ExpectedSalary is < 0 or > 9_999_999_999m)
            return Error.Validation("Salaries must be 0 or more.");
        if (!Enum.IsDefined(r.Source)) return Error.Validation("Pick a source.");
        return null;
    }

    private static void Apply(Candidate c, SaveCandidateRequest r, string? cnic)
    {
        c.FullName = r.FullName.Trim();
        c.Cnic = cnic;
        c.Phone = r.Phone.Trim();
        c.Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
        c.Qualification = string.IsNullOrWhiteSpace(r.Qualification) ? null : r.Qualification.Trim();
        c.ExperienceYears = r.ExperienceYears;
        c.CurrentSalary = r.CurrentSalary;
        c.ExpectedSalary = r.ExpectedSalary;
        c.Source = r.Source;
    }

    /// <summary>Allowed extension whose magic bytes agree with it, within the size limit; else null.</summary>
    private static async Task<string?> CheckCvAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > CvLimit) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        if (!CvExtensions.Contains(ext)) return null;

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header, ct);
        return FileSignature.Detect(header.AsSpan(0, read)) == ext ? ext : null;
    }
}
