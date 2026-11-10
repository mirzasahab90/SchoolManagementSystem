using Microsoft.AspNetCore.Http;
using SchoolManagementSystem.Application.Services.Students;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>Staff photo and documents. Access always goes through the campus-filtered staff record first.</summary>
public partial class StaffService
{
    public const long PhotoLimit = 2_097_152;
    public const long DocumentLimit = 10_485_760;
    private static readonly string[] PhotoTypes = [".jpg", ".png", ".webp"];
    private static readonly string[] DocumentTypes = [".jpg", ".png", ".webp", ".pdf"];

    public async Task<Result> SetPhotoAsync(int id, IFormFile file, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        var ext = await CheckFileAsync(file, PhotoLimit, PhotoTypes, ct);
        if (ext == null) return Error.Validation("Only JPG, PNG or WEBP images up to 2 MB.");

        var old = staff.PhotoPath;
        await using var stream = file.OpenReadStream();
        staff.PhotoPath = await storage.SaveAsync(Folder(staff), ext, stream, ct);
        await repositories.SaveChangesAsync(ct);
        if (old != null) await storage.DeleteAsync(old, ct);
        return Result.Ok("Photo updated.");
    }

    public async Task<Result<(Stream Content, string ContentType)>> GetPhotoAsync(int id, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff?.PhotoPath == null) return Error.NotFound("Photo");
        var stream = await storage.OpenAsync(staff.PhotoPath, ct);
        if (stream == null) return Error.NotFound("Photo");
        return (stream, FileSignature.ContentType(Path.GetExtension(staff.PhotoPath)));
    }

    public async Task<Result<StaffDocumentDto>> UploadDocumentAsync(int id, StaffDocumentType type, IFormFile file, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        if (!Enum.IsDefined(type)) return Error.Validation("Pick a document type.");
        var ext = await CheckFileAsync(file, DocumentLimit, DocumentTypes, ct);
        if (ext == null) return Error.Validation("Only JPG, PNG, WEBP or PDF files up to 10 MB.");

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(Folder(staff), ext, stream, ct);
        var name = Path.GetFileName(file.FileName);
        var doc = new StaffDocument
        {
            CampusId = staff.CampusId,
            StaffId = staff.Id,
            Type = type,
            FileName = name.Length is > 0 and <= 255 ? name : $"document{ext}",
            ContentType = FileSignature.ContentType(ext),
            SizeBytes = file.Length,
            StoredPath = stored,
            UploadedOn = DateTime.UtcNow,
            UploadedBy = currentUser.UserId
        };
        staff.Documents.Add(doc);
        TryActivate(staff);
        await repositories.SaveChangesAsync(ct);
        return new StaffDocumentDto(doc.Id, doc.Type, doc.FileName, doc.ContentType, doc.SizeBytes, doc.UploadedOn);
    }

    public async Task<Result<(Stream Content, string ContentType, string FileName)>> GetDocumentAsync(int id, int docId, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        var doc = staff?.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc == null) return Error.NotFound("Document");
        var stream = await storage.OpenAsync(doc.StoredPath, ct);
        if (stream == null) return Error.NotFound("Document");
        return (stream, doc.ContentType, doc.FileName);
    }

    public async Task<Result> DeleteDocumentAsync(int id, int docId, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        var doc = staff?.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc == null) return Error.NotFound("Document");
        repositories.Staff.Remove(doc); // soft delete; the file stays on disk
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Document deleted.");
    }

    private static string Folder(Staff staff) => $"{staff.CampusId}/staff/{staff.Id}";

    /// <summary>Allowed extension whose magic bytes agree with it, within the size limit; else null.</summary>
    private static async Task<string?> CheckFileAsync(IFormFile file, long limit, string[] allowed, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > limit) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        if (!allowed.Contains(ext)) return null;

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header, ct);
        return FileSignature.Detect(header.AsSpan(0, read)) == ext ? ext : null;
    }
}
