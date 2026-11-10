using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Fee heads, class-wise structure and settings. Main Campus rows apply everywhere; a campus row overrides them.</summary>
public class FeeSetupService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IFeeSetupService
{
    public static readonly Error MainCampusOnly = Error.Forbidden("Only Main Campus can change the Main Campus fee setup.");

    // ---------------------------------------------------------------- heads

    public async Task<Result<IReadOnlyList<FeeHeadDto>>> HeadsAsync(CancellationToken ct = default) =>
        (await repositories.FeeSetup.HeadsAsync(currentUser.ActiveCampusId, ct)).Select(ToDto).ToList();

    public async Task<Result<FeeHeadDto>> CreateHeadAsync(SaveFeeHeadRequest r, CancellationToken ct = default)
    {
        var owner = Owner(r.MainCampus);
        if (owner.Error != null) return owner.Error;
        var error = await ValidateHeadAsync(r, owner.CampusId, null, ct);
        if (error != null) return error;
        var head = new FeeHead { CampusId = owner.CampusId };
        Apply(head, r);
        repositories.FeeSetup.Add(head);
        await repositories.SaveChangesAsync(ct);
        return ToDto(head);
    }

    public async Task<Result<FeeHeadDto>> UpdateHeadAsync(int id, SaveFeeHeadRequest r, CancellationToken ct = default)
    {
        var head = await repositories.FeeSetup.GetHeadTrackedAsync(id, ct);
        if (head == null) return Error.NotFound("Fee head");
        var write = CheckWrite(head.CampusId);
        if (write != null) return write;
        var error = await ValidateHeadAsync(r, head.CampusId, id, ct);
        if (error != null) return error;
        Apply(head, r);
        await repositories.SaveChangesAsync(ct);
        return ToDto(head);
    }

    public async Task<Result> DeleteHeadAsync(int id, CancellationToken ct = default)
    {
        var head = await repositories.FeeSetup.GetHeadTrackedAsync(id, ct);
        if (head == null) return Error.NotFound("Fee head");
        var write = CheckWrite(head.CampusId);
        if (write != null) return write;
        if (await repositories.FeeSetup.HeadInUseAsync(id, ct))
            return Error.Validation("This head is used in the fee structure, a concession or a challan. Make it inactive instead.");
        head.IsDeleted = true;
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Fee head deleted.");
    }

    // ---------------------------------------------------------------- structure

    public async Task<Result<StructureGridDto>> StructureAsync(int sessionId, CancellationToken ct = default)
    {
        var campusId = currentUser.ActiveCampusId;
        var heads = (await repositories.FeeSetup.HeadsAsync(campusId, ct)).Select(ToDto).ToList();
        var classes = await repositories.FeeSetup.ClassesAsync(campusId, ct);
        var rows = await repositories.FeeSetup.StructureAsync(campusId, sessionId, ct);
        var main = rows.Where(x => x.CampusId == null).ToDictionary(x => (x.ClassId, x.FeeHeadId), x => x.Amount);
        var own = rows.Where(x => x.CampusId != null).ToDictionary(x => (x.ClassId, x.FeeHeadId), x => x.Amount);

        var grid = classes.Select(c => new StructureRowDto(c.Id, c.Name, heads.Select(h =>
        {
            decimal? m = main.TryGetValue((c.Id, h.Id), out var mv) ? mv : null;
            decimal? o = own.TryGetValue((c.Id, h.Id), out var ov) ? ov : null;
            return new StructureCellDto(h.Id, m, o, o ?? m);
        }).ToList())).ToList();
        return new StructureGridDto(sessionId, heads, grid);
    }

    public async Task<Result<StructureGridDto>> SaveStructureAsync(SaveStructureRequest r, CancellationToken ct = default)
    {
        var owner = Owner(r.MainCampus);
        if (owner.Error != null) return owner.Error;
        var rows = r.Rows ?? [];
        if (rows.Any(x => x.Amount is < 0 or > 10_000_000)) return Error.Validation("Amounts must be between 0 and 10,000,000.");

        var heads = (await repositories.FeeSetup.HeadsAsync(owner.CampusId, ct)).Select(h => h.Id).ToHashSet();
        var classes = (await repositories.FeeSetup.ClassesAsync(owner.CampusId, ct)).Select(c => c.Id).ToHashSet();
        if (rows.Any(x => !heads.Contains(x.FeeHeadId))) return Error.NotFound("Fee head");
        if (rows.Any(x => !classes.Contains(x.ClassId))) return Error.NotFound("Class");

        var existing = await repositories.FeeSetup.OwnedStructureTrackedAsync(owner.CampusId, r.SessionId, ct);
        foreach (var x in rows)
        {
            var row = existing.FirstOrDefault(e => e.ClassId == x.ClassId && e.FeeHeadId == x.FeeHeadId);
            if (x.Amount is not decimal amount)
            {
                if (row != null) repositories.FeeSetup.Remove(row);
                continue;
            }

            if (row == null)
            {
                row = new FeeStructure { CampusId = owner.CampusId, AcademicSessionId = r.SessionId, ClassId = x.ClassId, FeeHeadId = x.FeeHeadId };
                repositories.FeeSetup.Add(row);
                existing.Add(row);
            }

            row.Amount = decimal.Round(amount, 2);
        }

        await repositories.SaveChangesAsync(ct);
        return await StructureAsync(r.SessionId, ct);
    }

    // ---------------------------------------------------------------- settings

    public async Task<Result<FeeSettingsDto>> SettingsAsync(CancellationToken ct = default) =>
        await ResolveSettingsAsync(currentUser.ActiveCampusId, ct);

    /// <summary>Main Campus settings with the campus's fields laid over them.</summary>
    public async Task<FeeSettingsDto> ResolveSettingsAsync(int? campusId, CancellationToken ct = default)
    {
        var (main, own) = await repositories.FeeSetup.SettingsAsync(campusId, ct);
        string? headIds = own?.ConcessionHeadIds ?? main?.ConcessionHeadIds;
        return new FeeSettingsDto(
            own?.DueDay ?? main?.DueDay ?? 10,
            own?.LateFine ?? main?.LateFine ?? 0,
            own?.SiblingSecondPercent ?? main?.SiblingSecondPercent ?? 0,
            own?.SiblingThirdPlusPercent ?? main?.SiblingThirdPlusPercent ?? 0,
            own?.StaffChildPercent ?? main?.StaffChildPercent ?? 0,
            ParseIds(headIds),
            own?.BankName ?? main?.BankName,
            own?.AccountTitle ?? main?.AccountTitle,
            own?.AccountNo ?? main?.AccountNo,
            own?.ChallanNote ?? main?.ChallanNote,
            own != null,
            own == null ? [] : Overridden(own));
    }

    public async Task<Result<FeeSettingsDto>> SaveSettingsAsync(SaveFeeSettingsRequest r, CancellationToken ct = default)
    {
        var owner = Owner(r.MainCampus);
        if (owner.Error != null) return owner.Error;
        if (r.DueDay is < 1 or > 28) return Error.Validation("Due day must be between 1 and 28.");
        if (r.LateFine is < 0 or > 1_000_000) return Error.Validation("Late fine must be between 0 and 1,000,000.");
        decimal?[] pct = [r.SiblingSecondPercent, r.SiblingThirdPlusPercent, r.StaffChildPercent];
        if (pct.Any(p => p is < 0 or > 100)) return Error.Validation("Percentages must be between 0 and 100.");
        if (r.ConcessionHeadIds != null)
        {
            var heads = (await repositories.FeeSetup.HeadsAsync(owner.CampusId, ct)).Select(h => h.Id).ToHashSet();
            if (r.ConcessionHeadIds.Any(id => !heads.Contains(id))) return Error.NotFound("Fee head");
        }

        var row = await repositories.FeeSetup.OwnedSettingsTrackedAsync(owner.CampusId, ct);
        if (row == null)
        {
            row = new FeeSettings { CampusId = owner.CampusId };
            repositories.FeeSetup.Add(row);
        }

        var main = owner.CampusId == null;
        // Main Campus keeps a value for every field; a campus leaves fields empty to use the Main Campus value.
        row.DueDay = r.DueDay ?? (main ? row.DueDay ?? 10 : null);
        row.LateFine = r.LateFine ?? (main ? row.LateFine ?? 0 : null);
        row.SiblingSecondPercent = r.SiblingSecondPercent ?? (main ? row.SiblingSecondPercent ?? 0 : null);
        row.SiblingThirdPlusPercent = r.SiblingThirdPlusPercent ?? (main ? row.SiblingThirdPlusPercent ?? 0 : null);
        row.StaffChildPercent = r.StaffChildPercent ?? (main ? row.StaffChildPercent ?? 0 : null);
        row.ConcessionHeadIds = r.ConcessionHeadIds != null ? string.Join(",", r.ConcessionHeadIds.Distinct()) : main ? row.ConcessionHeadIds ?? "" : null;
        row.BankName = Clean(r.BankName, 100);
        row.AccountTitle = Clean(r.AccountTitle, 100);
        row.AccountNo = Clean(r.AccountNo, 100);
        row.ChallanNote = Clean(r.ChallanNote, 500);
        await repositories.SaveChangesAsync(ct);
        return await ResolveSettingsAsync(currentUser.ActiveCampusId, ct);
    }

    /// <summary>Drops the working campus's settings so it uses the Main Campus ones.</summary>
    public async Task<Result> DeleteCampusSettingsAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var row = await repositories.FeeSetup.OwnedSettingsTrackedAsync(campusId, ct);
        if (row == null) return Error.NotFound("Campus fee settings");
        row.IsDeleted = true;
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("The campus now uses the Main Campus fee settings.");
    }

    // ---------------------------------------------------------------- helpers

    private (int? CampusId, Error? Error) Owner(bool mainCampus)
    {
        if (mainCampus) return currentUser.IsHq ? (null, null) : (null, MainCampusOnly);
        return currentUser.ActiveCampusId is int campusId ? (campusId, null) : (null, Error.Validation("Select a campus."));
    }

    private Error? CheckWrite(int? owner) =>
        owner is int campusId
            ? currentUser.ActiveCampusId == campusId || (currentUser.IsHq && currentUser.ActiveCampusId == null) ? null : Error.NotFound("Fee head")
            : currentUser.IsHq ? null : MainCampusOnly;

    private async Task<Error?> ValidateHeadAsync(SaveFeeHeadRequest r, int? owner, int? id, CancellationToken ct)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length is 0 or > 60) return Error.Validation("Give the head a name (60 characters at most).");
        if (r.Frequency == FeeFrequency.Monthly && r.OneTimeMonth != null) return Error.Validation("Only one-time heads have a month.");
        if (r.OneTimeMonth is < 1 or > 12) return Error.Validation("Month must be between 1 and 12.");
        if (await repositories.FeeSetup.HeadNameTakenAsync(owner, name, id, ct)) return Error.Conflict("A fee head with this name already exists.");
        return null;
    }

    private static void Apply(FeeHead head, SaveFeeHeadRequest r)
    {
        head.Name = r.Name.Trim();
        head.Frequency = r.Frequency;
        head.OneTimeMonth = r.Frequency == FeeFrequency.OneTime ? r.OneTimeMonth : null;
        head.SortOrder = r.SortOrder;
        head.IsActive = r.IsActive;
    }

    /// <summary>camelCase names of the campus row's own (non-empty) fields.</summary>
    private static List<string> Overridden(FeeSettings s)
    {
        var list = new List<string>();
        if (s.DueDay != null) list.Add("dueDay");
        if (s.LateFine != null) list.Add("lateFine");
        if (s.SiblingSecondPercent != null) list.Add("siblingSecondPercent");
        if (s.SiblingThirdPlusPercent != null) list.Add("siblingThirdPlusPercent");
        if (s.StaffChildPercent != null) list.Add("staffChildPercent");
        if (s.ConcessionHeadIds != null) list.Add("concessionHeadIds");
        if (s.BankName != null) list.Add("bankName");
        if (s.AccountTitle != null) list.Add("accountTitle");
        if (s.AccountNo != null) list.Add("accountNo");
        if (s.ChallanNote != null) list.Add("challanNote");
        return list;
    }

    public static IReadOnlyList<int> ParseIds(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

    private static string? Clean(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }

    public static FeeHeadDto ToDto(FeeHead h) => new(h.Id, h.CampusId, h.Name, h.Frequency, h.OneTimeMonth, h.SortOrder, h.IsActive);
}
