using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Infrastructure.Seeding;

/// <summary>
/// Head-office academic defaults: the current session, Playgroup–Grade 10, common subjects with
/// stage-wise class lists, and sections A/B for Grade 1–5 on each campus. Each part is only seeded
/// when its table is empty, so running twice changes nothing.
/// </summary>
public static class AcademicSeeder
{
    private static readonly (string Code, string Name, ClassStage Stage)[] ClassList =
    [
        ("PG", "Playgroup", ClassStage.PrePrimary),
        ("NUR", "Nursery", ClassStage.PrePrimary),
        ("PREP", "Prep", ClassStage.PrePrimary),
        ("G1", "Grade 1", ClassStage.Primary),
        ("G2", "Grade 2", ClassStage.Primary),
        ("G3", "Grade 3", ClassStage.Primary),
        ("G4", "Grade 4", ClassStage.Primary),
        ("G5", "Grade 5", ClassStage.Primary),
        ("G6", "Grade 6", ClassStage.Middle),
        ("G7", "Grade 7", ClassStage.Middle),
        ("G8", "Grade 8", ClassStage.Middle),
        ("G9", "Grade 9", ClassStage.Secondary),
        ("G10", "Grade 10", ClassStage.Secondary),
    ];

    private static readonly (string Code, string Name, SubjectType Type)[] SubjectList =
    [
        ("ENG", "English", SubjectType.Theory),
        ("URDU", "Urdu", SubjectType.Theory),
        ("MATH", "Mathematics", SubjectType.Theory),
        ("SCI", "Science", SubjectType.Theory),
        ("SST", "Social Studies", SubjectType.Theory),
        ("ISL", "Islamiat", SubjectType.Theory),
        ("CS", "Computer Science", SubjectType.Theory),
        ("ART", "Art", SubjectType.Activity),
        ("PHY", "Physics", SubjectType.Theory),
        ("CHEM", "Chemistry", SubjectType.Theory),
        ("BIO", "Biology", SubjectType.Theory),
        ("PE", "Physical Education", SubjectType.Activity),
    ];

    /// <summary>Subjects per stage; a trailing "?" marks the subject optional.</summary>
    private static readonly Dictionary<ClassStage, string[]> StageSubjects = new()
    {
        [ClassStage.PrePrimary] = ["ENG", "URDU", "MATH", "ART"],
        [ClassStage.Primary] = ["ENG", "URDU", "MATH", "SCI", "SST", "ISL", "CS", "ART?", "PE"],
        [ClassStage.Middle] = ["ENG", "URDU", "MATH", "SCI", "SST", "ISL", "CS", "PE"],
        [ClassStage.Secondary] = ["ENG", "URDU", "MATH", "ISL", "PHY", "CHEM", "BIO?", "CS?", "PE"],
    };

    public static async Task SeedAsync(SchoolManagementSystemDbContext db, CancellationToken ct)
    {
        if (!await db.AcademicSessions.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.AcademicSessions.Add(new AcademicSession
            {
                Name = "2026-27",
                StartDate = new DateOnly(2026, 4, 1),
                EndDate = new DateOnly(2027, 3, 31),
                Status = SessionStatus.Active,
                IsCurrent = true
            });
        }

        if (!await db.Subjects.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.Subjects.AddRange(SubjectList.Select(s => new Subject { Code = s.Code, Name = s.Name, Type = s.Type }));
        }

        await db.SaveChangesAsync(ct);

        if (!await db.Classes.IgnoreQueryFilters().AnyAsync(ct))
        {
            var subjects = await db.Subjects.Where(s => s.CampusId == null).ToDictionaryAsync(s => s.Code, ct);
            var order = 1;
            foreach (var (code, name, stage) in ClassList)
            {
                var schoolClass = new SchoolClass { Code = code, Name = name, Stage = stage, DisplayOrder = order++ };
                var subjectOrder = 1;
                foreach (var entry in StageSubjects[stage])
                {
                    var optional = entry.EndsWith('?');
                    var subjectCode = entry.TrimEnd('?');
                    if (!subjects.TryGetValue(subjectCode, out var subject)) continue;
                    schoolClass.Subjects.Add(new ClassSubject
                    {
                        Subject = subject,
                        IsOptional = optional,
                        DisplayOrder = subjectOrder++
                    });
                }

                db.Classes.Add(schoolClass);
            }

            await db.SaveChangesAsync(ct);
        }

        if (!await db.Sections.IgnoreQueryFilters().AnyAsync(ct))
        {
            var session = await db.AcademicSessions.FirstOrDefaultAsync(s => s.CampusId == null && s.IsCurrent, ct);
            var primary = await db.Classes
                .Where(c => c.CampusId == null && c.Stage == ClassStage.Primary)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync(ct);
            var campuses = await db.Campuses.Where(c => c.IsActive).ToListAsync(ct);
            if (session == null) return;

            foreach (var campus in campuses)
            {
                foreach (var schoolClass in primary)
                {
                    foreach (var name in new[] { "A", "B" })
                    {
                        db.Sections.Add(new Section
                        {
                            CampusId = campus.Id,
                            AcademicSessionId = session.Id,
                            ClassId = schoolClass.Id,
                            Name = name,
                            Capacity = 35
                        });
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
