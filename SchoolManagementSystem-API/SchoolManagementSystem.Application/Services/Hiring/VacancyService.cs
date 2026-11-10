using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>The manual stage moves; Offered and Hired are reached only through the offer and hire endpoints.</summary>
public static class CandidatePipeline
{
    private static readonly Dictionary<CandidateStage, CandidateStage[]> Moves = new()
    {
        [CandidateStage.Applied] = [CandidateStage.Shortlisted, CandidateStage.Rejected, CandidateStage.Withdrawn],
        [CandidateStage.Shortlisted] = [CandidateStage.Interview, CandidateStage.Rejected, CandidateStage.Withdrawn],
        [CandidateStage.Interview] = [CandidateStage.Rejected, CandidateStage.Withdrawn],
        [CandidateStage.Offered] = [CandidateStage.Rejected, CandidateStage.Withdrawn],
        [CandidateStage.Rejected] = [CandidateStage.Applied],
        [CandidateStage.Withdrawn] = [CandidateStage.Applied],
        [CandidateStage.Hired] = []
    };

    public static IReadOnlyList<CandidateStage> Allowed(CandidateStage from) => Moves[from];
}

public class VacancyService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IVacancyService
{
    public async Task<Result<IReadOnlyList<VacancyDto>>> ListAsync(VacancyStatus? status, CancellationToken ct = default) =>
        await repositories.Vacancies.ListAsync(status, null, ct);

    public async Task<Result<VacancyDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = (await repositories.Vacancies.ListAsync(null, id, ct)).FirstOrDefault();
        return dto == null ? Error.NotFound("Vacancy") : dto;
    }

    public async Task<Result<VacancyDto>> CreateAsync(SaveVacancyRequest request, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var error = await ValidateAsync(request, ct);
        if (error != null) return error;

        var vacancy = new Vacancy { CampusId = campusId, Status = VacancyStatus.Open };
        Apply(vacancy, request);
        repositories.Vacancies.Add(vacancy);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(vacancy.Id, ct);
    }

    public async Task<Result<VacancyDto>> UpdateAsync(int id, SaveVacancyRequest request, CancellationToken ct = default)
    {
        var vacancy = await repositories.Vacancies.GetTrackedAsync(id, ct);
        if (vacancy == null) return Error.NotFound("Vacancy");
        var error = await ValidateAsync(request, ct);
        if (error != null) return error;

        var hired = await repositories.Vacancies.CountHiredAsync(id, ct);
        if (request.Positions < hired) return Error.Validation($"Positions cannot be fewer than the {hired} already hired.");

        Apply(vacancy, request);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<VacancyDto>> SetStatusAsync(int id, VacancyStatusRequest request, CancellationToken ct = default)
    {
        var vacancy = await repositories.Vacancies.GetTrackedAsync(id, ct);
        if (vacancy == null) return Error.NotFound("Vacancy");
        if (request.Status == VacancyStatus.Filled) return Error.Validation("A vacancy becomes Filled automatically when its positions are hired.");
        if (!Enum.IsDefined(request.Status)) return Error.Validation("Unknown status.");

        if (request.Status == VacancyStatus.Open && vacancy.Status != VacancyStatus.Open
            && vacancy.Positions <= await repositories.Vacancies.CountHiredAsync(id, ct))
        {
            return Error.Validation("All positions are hired. Raise the positions to reopen this vacancy.");
        }

        vacancy.Status = request.Status;
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var vacancy = await repositories.Vacancies.GetTrackedAsync(id, ct);
        if (vacancy == null) return Error.NotFound("Vacancy");
        if (await repositories.Vacancies.HasCandidatesAsync(id, ct))
            return Error.Validation("This vacancy has candidates. Close it instead.");
        repositories.Vacancies.Remove(vacancy);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Vacancy deleted.");
    }

    private async Task<Error?> ValidateAsync(SaveVacancyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Error.Validation("Title is required.");
        if (request.Title.Trim().Length > 150) return Error.Validation("Title is too long (150 characters at most).");
        if (request.Positions < 1) return Error.Validation("Positions must be at least 1.");
        if (request.Positions > 500) return Error.Validation("Positions must be 500 or fewer.");
        if (!Enum.IsDefined(request.Designation) || !Enum.IsDefined(request.Category)) return Error.Validation("Pick a designation and category.");
        if (request.Requirements?.Length > 2000) return Error.Validation("Requirements are too long (2000 characters at most).");

        var classIds = (request.ClassIds ?? []).Distinct().ToList();
        var subjectIds = (request.SubjectIds ?? []).Distinct().ToList();
        if (classIds.Count > 50 || subjectIds.Count > 50) return Error.Validation("Pick at most 50 classes and 50 subjects.");
        if (!await repositories.Vacancies.ClassesVisibleAsync(classIds, ct)) return Error.Validation("Pick classes of this campus.");
        if (!await repositories.Vacancies.SubjectsVisibleAsync(subjectIds, ct)) return Error.Validation("Pick subjects of this campus.");
        return null;
    }

    private static void Apply(Vacancy vacancy, SaveVacancyRequest request)
    {
        vacancy.Title = request.Title.Trim();
        vacancy.Designation = request.Designation;
        vacancy.Category = request.Category;
        vacancy.Positions = request.Positions;
        vacancy.ClassIds = VacancyRepository.JoinIds(request.ClassIds);
        vacancy.SubjectIds = VacancyRepository.JoinIds(request.SubjectIds);
        vacancy.Requirements = string.IsNullOrWhiteSpace(request.Requirements) ? null : request.Requirements.Trim();
        vacancy.ClosingDate = request.ClosingDate;
    }
}
