using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Infrastructure.Seeding;

/// <summary>Main Campus grade scale and result scheme; each seeded only when missing, so running twice changes nothing.</summary>
public static class ExamSeeder
{
    private static readonly (string Grade, decimal Min, bool Fail)[] Bands =
        [("A+", 90, false), ("A", 80, false), ("B", 70, false), ("C", 60, false), ("D", 50, false), ("E", 40, false), ("F", 0, true)];

    public static async Task SeedAsync(SchoolManagementSystemDbContext db, CancellationToken ct = default)
    {
        if (!await db.GradeScales.IgnoreQueryFilters().AnyAsync(s => s.CampusId == null && !s.IsDeleted, ct))
        {
            var scale = new GradeScale { CampusId = null, Name = "Standard" };
            foreach (var (grade, min, fail) in Bands) scale.Bands.Add(new GradeBand { Grade = grade, MinPercent = min, IsFail = fail });
            db.GradeScales.Add(scale);
        }

        if (!await db.ResultSchemes.IgnoreQueryFilters()
                .AnyAsync(s => s.CampusId == null && s.AcademicSessionId == null && s.ClassId == null && !s.IsDeleted, ct))
        {
            db.ResultSchemes.Add(new ResultScheme
            {
                CampusId = null, MonthlyWeight = 20, MidTermWeight = 30, FinalWeight = 50, OtherWeight = 0, MustPassFinal = true
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
