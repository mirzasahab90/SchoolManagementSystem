using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>Interview rounds and offers.</summary>
public partial class CandidateService
{
    public async Task<Result<CandidateProfileDto>> ScheduleInterviewAsync(int id, ScheduleInterviewRequest request, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        if (candidate.Stage is not (CandidateStage.Shortlisted or CandidateStage.Interview))
            return Error.Validation("Shortlist the candidate before scheduling an interview.");

        var error = await ValidateRoundAsync(candidate, request.Name, request.InterviewerUserId, ct);
        if (error != null) return error;

        candidate.Interviews.Add(new InterviewRound
        {
            CampusId = candidate.CampusId,
            Name = request.Name.Trim(),
            ScheduledAt = request.ScheduledAt.ToUniversalTime(),
            InterviewerUserId = request.InterviewerUserId,
            Result = InterviewResult.Scheduled
        });
        if (candidate.Stage == CandidateStage.Shortlisted) SetStage(candidate, CandidateStage.Interview, null);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<CandidateProfileDto>> UpdateInterviewAsync(int id, int roundId, UpdateInterviewRequest request, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        var round = candidate.Interviews.FirstOrDefault(r => r.Id == roundId);
        if (round == null) return Error.NotFound("Interview round");
        if (candidate.Stage is not (CandidateStage.Interview or CandidateStage.Offered))
            return Error.Validation("Interview rounds can only change while the candidate is in Interview or Offered.");

        if (!Enum.IsDefined(request.Result)) return Error.Validation("Unknown result.");
        if (request.Rating is < 1 or > 5) return Error.Validation("Rating must be from 1 to 5.");
        if (request.Result != InterviewResult.Scheduled && request.Rating == null) return Error.Validation("Give a rating from 1 to 5 with the result.");
        if (request.Notes?.Length > 2000) return Error.Validation("Notes are too long (2000 characters at most).");
        if (candidate.Stage == CandidateStage.Offered && round.Result == InterviewResult.Pass && request.Result != InterviewResult.Pass
            && candidate.Interviews.Count(r => r.Result == InterviewResult.Pass) == 1)
        {
            return Error.Validation("This is the passed round behind the offer; its result cannot change.");
        }

        if (request.InterviewerUserId != round.InterviewerUserId || request.Name != round.Name)
        {
            var error = await ValidateRoundAsync(candidate, request.Name, request.InterviewerUserId, ct);
            if (error != null) return error;
        }

        round.Name = request.Name.Trim();
        round.ScheduledAt = request.ScheduledAt.ToUniversalTime();
        round.InterviewerUserId = request.InterviewerUserId;
        round.Result = request.Result;
        round.Rating = request.Rating;
        round.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<CandidateProfileDto>> DeleteInterviewAsync(int id, int roundId, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        var round = candidate.Interviews.FirstOrDefault(r => r.Id == roundId);
        if (round == null) return Error.NotFound("Interview round");
        if (round.Result != InterviewResult.Scheduled) return Error.Validation("Only a round that is still Scheduled can be deleted.");

        repositories.Candidates.Remove(round);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<CandidateProfileDto>> MakeOfferAsync(int id, MakeOfferRequest request, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");

        var latest = LatestOffer(candidate);
        if (candidate.Stage == CandidateStage.Offered && latest?.Status == OfferStatus.Accepted)
            return Error.Validation("The offer is already accepted. Hire the candidate.");
        if (candidate.Stage is not (CandidateStage.Interview or CandidateStage.Offered))
            return Error.Validation("Make an offer after the interviews.");
        if (!candidate.Interviews.Any(r => r.Result == InterviewResult.Pass))
            return Error.Validation("Record a passed interview first.");
        if (request.OfferedSalary <= 0 || request.OfferedSalary > 9_999_999_999m) return Error.Validation("The salary must be greater than 0.");
        if (!Enum.IsDefined(request.Designation) || !Enum.IsDefined(request.EmploymentType))
            return Error.Validation("Pick a designation and employment type.");

        foreach (var pending in candidate.Offers.Where(o => o.Status == OfferStatus.Pending))
        {
            pending.Status = OfferStatus.Withdrawn;
            pending.RespondedOn = DateTime.UtcNow;
        }

        candidate.Offers.Add(new Offer
        {
            CampusId = candidate.CampusId,
            Designation = request.Designation,
            EmploymentType = request.EmploymentType,
            OfferedSalary = request.OfferedSalary,
            ProposedJoiningDate = request.ProposedJoiningDate,
            Status = OfferStatus.Pending
        });
        if (candidate.Stage == CandidateStage.Interview) SetStage(candidate, CandidateStage.Offered, null);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<CandidateProfileDto>> RespondOfferAsync(int id, OfferStatus response, CancellationToken ct = default)
    {
        var candidate = await repositories.Candidates.GetTrackedAsync(id, ct);
        if (candidate == null) return Error.NotFound("Candidate");
        var offer = LatestOffer(candidate);
        if (candidate.Stage != CandidateStage.Offered || offer == null || offer.Status != OfferStatus.Pending)
            return Error.Validation("There is no pending offer.");

        offer.Status = response;
        offer.RespondedOn = DateTime.UtcNow;
        candidate.ModifiedOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    /// <summary>Round name present, interviewer active and allowed on the candidate's campus.</summary>
    private async Task<Error?> ValidateRoundAsync(Candidate candidate, string? name, int interviewerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("Round name is required.");
        if (name.Trim().Length > 60) return Error.Validation("Round name is too long (60 characters at most).");
        if (!await repositories.Users.CanInterviewAsync(interviewerId, candidate.CampusId, ct))
            return Error.Validation("Pick an active interviewer who works on this campus.");
        return null;
    }
}
