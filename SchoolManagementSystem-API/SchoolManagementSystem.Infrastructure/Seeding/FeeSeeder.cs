using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Infrastructure.Seeding;

/// <summary>Main Campus fee heads and fee settings; seeded only when missing, so running twice changes nothing.</summary>
public static class FeeSeeder
{
    private static readonly (string Name, FeeFrequency Frequency, int? Month, int Order, bool Active)[] Heads =
    [
        ("Tuition", FeeFrequency.Monthly, null, 1, true),
        ("Admission", FeeFrequency.OneTime, null, 2, true),
        ("Annual Fund", FeeFrequency.OneTime, 4, 3, true),
        ("Exam", FeeFrequency.OneTime, null, 4, false),
    ];

    public static async Task SeedAsync(SchoolManagementSystemDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FeeHeads.IgnoreQueryFilters().Where(h => h.CampusId == null && !h.IsDeleted)
            .Select(h => h.Name).ToListAsync(ct);
        if (existing.Count == 0)
        {
            foreach (var (name, frequency, month, order, active) in Heads)
                db.FeeHeads.Add(new FeeHead { Name = name, Frequency = frequency, OneTimeMonth = month, SortOrder = order, IsActive = active });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.FeeSettings.IgnoreQueryFilters().AnyAsync(s => s.CampusId == null && !s.IsDeleted, ct))
        {
            var tuition = await db.FeeHeads.IgnoreQueryFilters()
                .Where(h => h.CampusId == null && !h.IsDeleted && h.Name == "Tuition").Select(h => (int?)h.Id).FirstOrDefaultAsync(ct);
            db.FeeSettings.Add(new FeeSettings
            {
                CampusId = null, DueDay = 10, LateFine = 300, SiblingSecondPercent = 0, SiblingThirdPlusPercent = 0, StaffChildPercent = 0,
                ConcessionHeadIds = tuition?.ToString() ?? ""
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
