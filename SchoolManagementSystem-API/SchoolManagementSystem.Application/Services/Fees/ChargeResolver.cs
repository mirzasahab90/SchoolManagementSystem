using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>What a student is charged for a month: resolved amounts, concessions, sibling place and settings, fed to <see cref="FeeCalculator"/>.</summary>
public class ChargeResolver(SchoolManagementSystemRepositories repositories, FeeSetupService setup)
{
    private readonly Dictionary<(int Campus, int Session), (List<FeeHead> Heads, List<FeeStructure> Rows)> _cache = new();
    private readonly Dictionary<int, FeeSettingsDto> _settings = new();

    public async Task<FeeSettingsDto> SettingsAsync(int campusId, CancellationToken ct = default)
    {
        if (!_settings.TryGetValue(campusId, out var s)) _settings[campusId] = s = await setup.ResolveSettingsAsync(campusId, ct);
        return s;
    }

    /// <summary>Campus row → Main Campus row → null.</summary>
    public async Task<decimal?> AmountAsync(int campusId, int sessionId, int classId, int headId, CancellationToken ct = default)
    {
        var (_, rows) = await LoadAsync(campusId, sessionId, ct);
        return Amount(rows, classId, headId);
    }

    /// <summary>True when the class has at least one Monthly head with an amount.</summary>
    public async Task<bool> HasStructureAsync(int campusId, int sessionId, int classId, CancellationToken ct = default)
    {
        var (heads, rows) = await LoadAsync(campusId, sessionId, ct);
        return heads.Any(h => h.IsActive && h.Frequency == FeeFrequency.Monthly && Amount(rows, classId, h.Id) is not null);
    }

    public async Task<IReadOnlyList<HeadCharge>> ChargesAsync(
        int campusId, int sessionId, int classId, DateOnly month, bool firstChallan, CancellationToken ct = default)
    {
        var (heads, rows) = await LoadAsync(campusId, sessionId, ct);
        var list = new List<HeadCharge>();
        foreach (var h in heads.Where(h => h.IsActive))
        {
            var due = h.Frequency == FeeFrequency.Monthly
                      || (h.OneTimeMonth is int m ? m == month.Month : firstChallan);
            if (!due || Amount(rows, classId, h.Id) is not decimal amount || amount <= 0) continue;
            list.Add(new HeadCharge(h.Id, h.Name, h.Frequency, amount));
        }

        return list;
    }

    /// <summary>Head lines for a student's enrollment and month (used by preview, generate and single challans).</summary>
    public async Task<IReadOnlyList<LineCalc>> LinesAsync(
        int studentId, int campusId, DateOnly admissionDate, int sessionId, int classId, DateOnly month, AcademicSession session,
        CancellationToken ct = default)
    {
        var first = admissionDate >= session.StartDate && admissionDate <= session.EndDate
                    && !await repositories.Concessions.HasVoucherInSessionAsync(studentId, sessionId, ct);
        var charges = await ChargesAsync(campusId, sessionId, classId, month, first, ct);
        if (charges.Count == 0) return [];
        var concessions = (await repositories.Concessions.ActiveForStudentAsync(studentId, sessionId, ct))
            .Select(c => new ConcessionCandidate(c.Kind, c.FeeHeadId, c.ValueType, c.Value, c.Reason)).ToList();
        var position = await repositories.Concessions.SiblingPositionAsync(studentId, campusId, sessionId, ct);
        return FeeCalculator.Lines(charges, concessions, position, await SettingsAsync(campusId, ct));
    }

    private static decimal? Amount(List<FeeStructure> rows, int classId, int headId) =>
        rows.FirstOrDefault(r => r.CampusId != null && r.ClassId == classId && r.FeeHeadId == headId)?.Amount
        ?? rows.FirstOrDefault(r => r.CampusId == null && r.ClassId == classId && r.FeeHeadId == headId)?.Amount;

    private async Task<(List<FeeHead> Heads, List<FeeStructure> Rows)> LoadAsync(int campusId, int sessionId, CancellationToken ct)
    {
        if (_cache.TryGetValue((campusId, sessionId), out var hit)) return hit;
        var heads = await repositories.FeeSetup.HeadsAsync(campusId, ct);
        var rows = await repositories.FeeSetup.StructureAsync(campusId, sessionId, ct);
        return _cache[(campusId, sessionId)] = (heads, rows);
    }
}
