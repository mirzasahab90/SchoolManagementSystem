using SchoolManagementSystem.Domain.DTOs.Exams;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Application.Services.Exams;

public record PaperCalc(int PaperId, int SubjectId, string SubjectName, decimal Total, decimal Pass, bool HasPractical);

public record MarkCalc(int PaperId, int StudentId, decimal? Theory, decimal? Practical, MarkStatus Status);

public record StudentCalc(
    int StudentId, decimal? Obtained, decimal? Total, decimal? Percent, string? Grade, bool? Passed, int? Position, IReadOnlyList<SubjectCalc> Subjects);

/// <summary>Annual result: type averages weighted by the scheme; types without a published exam drop out and the rest scale to 100.</summary>
public static class AnnualCalculator
{
    public static AnnualCalc Compute(
        IReadOnlyDictionary<ExamType, IReadOnlyList<decimal>> percentsByType, SchemeDto scheme, bool passedFinal, IReadOnlyList<GradeBandDto> bands)
    {
        var weights = new Dictionary<ExamType, int>
        {
            [ExamType.Monthly] = scheme.MonthlyWeight, [ExamType.MidTerm] = scheme.MidTermWeight,
            [ExamType.Final] = scheme.FinalWeight, [ExamType.Other] = scheme.OtherWeight
        };
        var typePercents = new Dictionary<ExamType, decimal?>();
        var averages = new Dictionary<ExamType, decimal>();
        foreach (var (type, w) in weights.Where(x => x.Value > 0))
        {
            if (percentsByType.TryGetValue(type, out var list) && list.Count > 0)
            {
                averages[type] = ResultCalculator.Round(list.Average());
                typePercents[type] = averages[type];
            }
            else
            {
                typePercents[type] = null;
            }
        }

        var effective = new Dictionary<ExamType, decimal>();
        decimal sum = averages.Keys.Sum(t => weights[t]);
        if (sum == 0) return new AnnualCalc(null, null, null, typePercents, effective);
        decimal percent = 0;
        foreach (var (type, avg) in averages)
        {
            effective[type] = ResultCalculator.Round(weights[type] * 100m / sum);
            percent += avg * weights[type] / sum;
        }

        percent = ResultCalculator.Round(percent);
        var (grade, isFail) = ResultCalculator.GradeFor(percent, bands);
        return new AnnualCalc(percent, grade, !isFail && (!scheme.MustPassFinal || passedFinal), typePercents, effective);
    }
}

/// <summary>Pure exam-result rules (no I/O): subject results, overall result, competition-ranked positions.</summary>
public static class ResultCalculator
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>The band with the highest minimum not above the percentage.</summary>
    public static (string Grade, bool IsFail) GradeFor(decimal percent, IReadOnlyList<GradeBandDto> bands)
    {
        var band = bands.Where(b => b.MinPercent <= percent).OrderByDescending(b => b.MinPercent).FirstOrDefault()
                   ?? bands.OrderBy(b => b.MinPercent).FirstOrDefault();
        return band == null ? ("-", false) : (band.Grade, band.IsFail);
    }

    public static IReadOnlyList<StudentCalc> Compute(
        IReadOnlyList<PaperCalc> papers, IReadOnlyCollection<int> studentIds, IReadOnlyList<MarkCalc> marks, IReadOnlyList<GradeBandDto> bands)
    {
        var byStudent = marks.GroupBy(m => m.StudentId).ToDictionary(g => g.Key, g => g.ToDictionary(m => m.PaperId));
        var results = new List<StudentCalc>();
        foreach (var sid in studentIds)
        {
            byStudent.TryGetValue(sid, out var mine);
            mine ??= [];
            var subjects = new List<SubjectCalc>();
            decimal obtained = 0, total = 0;
            var allPassed = true;
            var counted = 0;
            foreach (var p in papers)
            {
                if (!mine.TryGetValue(p.PaperId, out var m))
                {
                    subjects.Add(new SubjectCalc(p.SubjectId, p.SubjectName, null, null, null, p.Total, null, null, null, null));
                    allPassed = false; // not entered yet
                    continue;
                }

                if (m.Status == MarkStatus.Exempted)
                {
                    subjects.Add(new SubjectCalc(p.SubjectId, p.SubjectName, null, null, null, p.Total, null, null, null, MarkStatus.Exempted));
                    continue;
                }

                var got = m.Status == MarkStatus.Absent ? 0 : (m.Theory ?? 0) + (m.Practical ?? 0);
                var pct = Round(got * 100 / p.Total);
                var passed = m.Status == MarkStatus.Present && got >= p.Pass;
                subjects.Add(new SubjectCalc(p.SubjectId, p.SubjectName, m.Theory, m.Practical, got, p.Total, pct, GradeFor(pct, bands).Grade, passed, m.Status));
                obtained += got;
                total += p.Total;
                allPassed &= passed;
                counted++;
            }

            if (counted == 0)
            {
                results.Add(new StudentCalc(sid, null, null, null, null, null, null, subjects));
                continue;
            }

            var percent = Round(obtained * 100 / total);
            var complete = subjects.All(s => s.Status != null);
            results.Add(new StudentCalc(sid, obtained, total, percent, GradeFor(percent, bands).Grade, complete ? allPassed : null, null, subjects));
        }

        return Rank(results);
    }

    /// <summary>Competition ranking (1, 2, 2, 4) by percentage among passed students.</summary>
    public static IReadOnlyList<StudentCalc> Rank(IReadOnlyList<StudentCalc> results)
    {
        var passed = results.Where(r => r.Passed == true && r.Percent != null).OrderByDescending(r => r.Percent).ToList();
        var positions = new Dictionary<int, int>();
        for (var i = 0; i < passed.Count; i++)
        {
            positions[passed[i].StudentId] = i > 0 && passed[i].Percent == passed[i - 1].Percent
                ? positions[passed[i - 1].StudentId]
                : i + 1;
        }

        return results.Select(r => r with { Position = positions.TryGetValue(r.StudentId, out var p) ? p : null }).ToList();
    }
}
