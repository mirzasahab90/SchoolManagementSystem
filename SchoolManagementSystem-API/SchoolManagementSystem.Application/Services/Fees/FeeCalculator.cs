using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Pure fee maths: turns charges and concession candidates into challan head lines. No I/O.</summary>
public static class FeeCalculator
{
    /// <summary>
    /// One line per charge. Per line the largest discount wins (Manual, StaffChild, Sibling); ties prefer Manual, then StaffChild.
    /// Manual / StaffChild with no head apply to Monthly heads (StaffChild: to the settings' concession heads); sibling discounts apply
    /// to the settings' concession heads from the second child on. Discounts are whole rupees.
    /// </summary>
    public static IReadOnlyList<LineCalc> Lines(
        IReadOnlyList<HeadCharge> charges, IReadOnlyList<ConcessionCandidate> concessions, int siblingPosition, FeeSettingsDto settings)
    {
        var concessionHeads = settings.ConcessionHeadIds.ToHashSet();
        var siblingPercent = siblingPosition switch { <= 1 => 0m, 2 => settings.SiblingSecondPercent, _ => settings.SiblingThirdPlusPercent };
        var lines = new List<LineCalc>(charges.Count);

        foreach (var c in charges)
        {
            var options = new List<(decimal Amount, int Rank, string Note)>();
            foreach (var k in concessions)
            {
                var applies = k.FeeHeadId is int head
                    ? head == c.FeeHeadId
                    : k.Kind == ConcessionKind.StaffChild ? concessionHeads.Contains(c.FeeHeadId) : c.Frequency == FeeFrequency.Monthly;
                if (!applies) continue;
                var amount = Discount(c.Amount, k.ValueType, k.Value);
                options.Add(k.Kind == ConcessionKind.Manual
                    ? (amount, 0, Truncate($"Manual: {k.Reason}"))
                    : (amount, 1, k.ValueType == ConcessionValueType.Percent ? $"Staff child {Pct(k.Value)}%" : "Staff child"));
            }

            if (siblingPercent > 0 && concessionHeads.Contains(c.FeeHeadId))
                options.Add((Discount(c.Amount, ConcessionValueType.Percent, siblingPercent), 2, $"Sibling {Pct(siblingPercent)}%"));

            var best = options.Where(o => o.Amount > 0).OrderByDescending(o => o.Amount).ThenBy(o => o.Rank).FirstOrDefault();
            var discount = best.Note == null ? 0 : best.Amount;
            lines.Add(new LineCalc(c.FeeHeadId, c.Name, c.Amount, discount, best.Note, c.Amount - discount));
        }

        return lines;
    }

    public static decimal Discount(decimal amount, ConcessionValueType type, decimal value)
    {
        var raw = type == ConcessionValueType.Percent ? amount * value / 100m : Math.Min(value, amount);
        return Math.Min(amount, decimal.Round(raw, 0, MidpointRounding.AwayFromZero));
    }

    private static string Pct(decimal v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string Truncate(string s) => s.Length > 80 ? s[..80] : s;
}
