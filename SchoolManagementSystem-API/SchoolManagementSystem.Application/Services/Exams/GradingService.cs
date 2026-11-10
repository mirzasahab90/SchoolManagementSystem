using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Exams;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Application.Services.Exams;

/// <summary>Grade scales and annual result schemes. Campus rows override the Main Campus defaults.</summary>
public class GradingService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IGradingService
{
    private static readonly Error WeightsNot100 = Error.Validation("Weights must add up to 100.");
    private static readonly Error MainCampusOnly = Error.Forbidden("Only Main Campus can change the Main Campus defaults.");

    public static readonly SchemeDto BuiltInScheme = new(null, null, null, null, 20, 30, 50, 0, true);

    public async Task<Result<GradeScaleDto>> GetScaleAsync(CancellationToken ct = default)
    {
        var scale = await repositories.Grading.ResolveScaleAsync(currentUser.ActiveCampusId, ct);
        return scale == null ? Error.NotFound("Grade scale") : ToDto(scale);
    }

    public async Task<Result<GradeScaleDto>> SaveScaleAsync(SaveScaleRequest request, CancellationToken ct = default)
    {
        var owner = await OwnerAsync(request.MainCampus);
        if (owner.Error != null) return owner.Error;
        var error = ValidateBands(request);
        if (error != null) return error;

        var scale = await repositories.Grading.GetOwnedTrackedAsync(owner.CampusId, ct);
        if (scale == null)
        {
            scale = new GradeScale { CampusId = owner.CampusId };
            repositories.Grading.Add(scale);
        }
        else
        {
            repositories.Grading.RemoveBands(scale.Bands.ToList());
            scale.Bands.Clear();
        }

        scale.Name = request.Name.Trim();
        foreach (var b in request.Bands.OrderByDescending(b => b.MinPercent))
            scale.Bands.Add(new GradeBand { Grade = b.Grade.Trim(), MinPercent = b.MinPercent, IsFail = b.IsFail });
        await repositories.SaveChangesAsync(ct);
        return ToDto(scale);
    }

    /// <summary>Removes the working campus's own scale so it falls back to the Main Campus one.</summary>
    public async Task<Result> DeleteCampusScaleAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var scale = await repositories.Grading.GetOwnedTrackedAsync(campusId, ct);
        if (scale == null) return Error.NotFound("Campus grade scale");
        repositories.Grading.Remove(scale);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("The campus now uses the Main Campus scale.");
    }

    public async Task<Result<SchemesDto>> GetSchemesAsync(int? sessionId, int? classId, CancellationToken ct = default)
    {
        var campusId = currentUser.ActiveCampusId;
        var all = await repositories.Grading.SchemesForAsync(campusId, ct);
        var session = sessionId ?? (await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct))?.Id;
        var effective = Resolve(all, campusId, session, classId);
        var owned = all.Where(s => s.CampusId == campusId || (currentUser.IsHq && s.CampusId == null)).Select(ToDto).ToList();
        return new SchemesDto(effective, owned);
    }

    public async Task<Result<SchemeDto>> SaveSchemeAsync(SaveSchemeRequest r, CancellationToken ct = default)
    {
        var owner = await OwnerAsync(r.MainCampus);
        if (owner.Error != null) return owner.Error;
        int[] w = [r.MonthlyWeight, r.MidTermWeight, r.FinalWeight, r.OtherWeight];
        if (w.Any(x => x is < 0 or > 100) || w.Sum() != 100) return WeightsNot100;

        var scheme = await repositories.Grading.FindSchemeTrackedAsync(owner.CampusId, r.SessionId, r.ClassId, ct);
        if (scheme == null)
        {
            scheme = new ResultScheme { CampusId = owner.CampusId, AcademicSessionId = r.SessionId, ClassId = r.ClassId };
            repositories.Grading.Add(scheme);
        }

        scheme.MonthlyWeight = r.MonthlyWeight;
        scheme.MidTermWeight = r.MidTermWeight;
        scheme.FinalWeight = r.FinalWeight;
        scheme.OtherWeight = r.OtherWeight;
        scheme.MustPassFinal = r.MustPassFinal;
        await repositories.SaveChangesAsync(ct);
        return ToDto(scheme);
    }

    public async Task<Result> DeleteSchemeAsync(int id, CancellationToken ct = default)
    {
        var scheme = await repositories.Grading.GetSchemeTrackedAsync(id, ct);
        if (scheme == null) return Error.NotFound("Scheme");
        if (scheme.CampusId == null && !currentUser.IsHq) return MainCampusOnly;
        repositories.Grading.Remove(scheme);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Scheme deleted.");
    }

    /// <summary>Bands of the scale a campus uses (campus scale, else Main Campus).</summary>
    public async Task<IReadOnlyList<GradeBandDto>> BandsForAsync(int campusId, CancellationToken ct = default)
    {
        var scale = await repositories.Grading.ResolveScaleAsync(campusId, ct);
        return scale == null ? [] : ToDto(scale).Bands;
    }

    /// <summary>Resolution: campus+session+class → campus+session → campus default → Main Campus+session+class → Main Campus+session → Main Campus default.</summary>
    public async Task<SchemeDto> SchemeForAsync(int campusId, int sessionId, int classId, CancellationToken ct = default) =>
        Resolve(await repositories.Grading.SchemesForAsync(campusId, ct), campusId, sessionId, classId);

    private static SchemeDto Resolve(List<ResultScheme> all, int? campusId, int? sessionId, int? classId)
    {
        (int? Campus, int? Session, int? Class)[] order =
        [
            (campusId, sessionId, classId), (campusId, sessionId, null), (campusId, null, classId), (campusId, null, null),
            (null, sessionId, classId), (null, sessionId, null), (null, null, classId), (null, null, null)
        ];
        foreach (var (c, s, k) in order)
        {
            var hit = all.FirstOrDefault(x => x.CampusId == c && x.AcademicSessionId == s && x.ClassId == k);
            if (hit != null) return ToDto(hit);
        }

        return BuiltInScheme;
    }

    private async Task<(int? CampusId, Error? Error)> OwnerAsync(bool mainCampus)
    {
        await Task.CompletedTask;
        if (mainCampus) return currentUser.IsHq ? (null, null) : (null, MainCampusOnly);
        return currentUser.ActiveCampusId is int campusId ? (campusId, null) : (null, Error.Validation("Select a campus."));
    }

    private static Error? ValidateBands(SaveScaleRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 60) return Error.Validation("Give the scale a name (60 characters at most).");
        var bands = r.Bands ?? [];
        if (bands.Count is 0 or > 20) return Error.Validation("Add between 1 and 20 grades.");
        if (bands.Any(b => string.IsNullOrWhiteSpace(b.Grade) || b.Grade.Trim().Length > 5)) return Error.Validation("Grades are 1 to 5 characters.");
        if (bands.Select(b => b.Grade.Trim().ToUpperInvariant()).Distinct().Count() != bands.Count) return Error.Validation("Each grade can appear once.");
        if (bands.Any(b => b.MinPercent is < 0 or > 100)) return Error.Validation("Minimum percentages are between 0 and 100.");
        if (bands.Select(b => b.MinPercent).Distinct().Count() != bands.Count) return Error.Validation("Each minimum percentage can appear once.");
        if (bands.All(b => b.MinPercent != 0)) return Error.Validation("One grade must start at 0%.");
        if (!bands.Any(b => b.IsFail)) return Error.Validation("Mark at least one grade as a fail grade.");
        return null;
    }

    private static GradeScaleDto ToDto(GradeScale s) =>
        new(s.Id, s.CampusId, s.Name, s.Bands.OrderByDescending(b => b.MinPercent).Select(b => new GradeBandDto(b.Grade, b.MinPercent, b.IsFail)).ToList());

    private static SchemeDto ToDto(ResultScheme s) =>
        new(s.Id, s.CampusId, s.AcademicSessionId, s.ClassId, s.MonthlyWeight, s.MidTermWeight, s.FinalWeight, s.OtherWeight, s.MustPassFinal);
}
