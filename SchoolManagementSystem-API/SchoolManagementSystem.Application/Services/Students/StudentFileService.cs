using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Students;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Services.Students;

/// <summary>Recognises allowed file types from their first bytes, so a renamed file cannot pass as another type.</summary>
public static class FileSignature
{
    public static string? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        if (h.Length >= 5 && h[..5].SequenceEqual("%PDF-"u8)) return ".pdf";
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })) return ".doc";
        if (h.Length >= 4 && h[..4].SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) return ".docx";
        return null;
    }

    public static string ContentType(string extension) => extension switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}

/// <summary>Student photo and documents. Access always goes through the campus-filtered student first.</summary>
public class StudentFileService(
    SchoolManagementSystemRepositories repositories,
    SchoolManagementSystemDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser) : IStudentFileService
{
    public const long PhotoLimit = 2_097_152;
    public const long DocumentLimit = 10_485_760;

    private static readonly Error PhotoRejected = Error.Validation("Only JPG, PNG or WEBP images up to 2 MB.");
    private static readonly Error DocumentRejected = Error.Validation("Only JPG, PNG, WEBP or PDF files up to 10 MB.");

    public async Task<Result> SetPhotoAsync(int studentId, IFormFile file, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");

        var ext = await CheckAsync(file, PhotoLimit, allowPdf: false, ct);
        if (ext == null) return PhotoRejected;

        var old = student.PhotoPath;
        await using var stream = file.OpenReadStream();
        student.PhotoPath = await storage.SaveAsync(student.CampusId, student.Id, ext, stream, ct);
        student.ModifiedOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct);
        if (old != null) await storage.DeleteAsync(old, ct);
        return Result.Ok("Photo updated.");
    }

    public async Task<Result<(Stream Content, string ContentType)>> GetPhotoAsync(int studentId, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student?.PhotoPath == null) return Error.NotFound("Photo");
        var stream = await storage.OpenAsync(student.PhotoPath, ct);
        if (stream == null) return Error.NotFound("Photo");
        return (stream, FileSignature.ContentType(Path.GetExtension(student.PhotoPath)));
    }

    public async Task<Result<StudentDocumentDto>> UploadDocumentAsync(int studentId, DocumentType type, IFormFile file, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");

        var ext = await CheckAsync(file, DocumentLimit, allowPdf: true, ct);
        if (ext == null) return DocumentRejected;

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(student.CampusId, student.Id, ext, stream, ct);
        var doc = new StudentDocument
        {
            CampusId = student.CampusId,
            StudentId = student.Id,
            Type = type,
            FileName = Path.GetFileName(file.FileName),
            ContentType = FileSignature.ContentType(ext),
            SizeBytes = file.Length,
            StoredPath = stored,
            UploadedOn = DateTime.UtcNow,
            UploadedBy = currentUser.UserId
        };
        db.StudentDocuments.Add(doc);
        await repositories.SaveChangesAsync(ct);
        return new StudentDocumentDto(doc.Id, doc.Type, doc.FileName, doc.ContentType, doc.SizeBytes, doc.UploadedOn);
    }

    public async Task<Result<(Stream Content, string ContentType, string FileName)>> GetDocumentAsync(int studentId, int docId, CancellationToken ct = default)
    {
        var doc = await FindDocumentAsync(studentId, docId, ct);
        if (doc == null) return Error.NotFound("Document");
        var stream = await storage.OpenAsync(doc.StoredPath, ct);
        if (stream == null) return Error.NotFound("Document");
        return (stream, doc.ContentType, doc.FileName);
    }

    public async Task<Result> DeleteDocumentAsync(int studentId, int docId, CancellationToken ct = default)
    {
        var doc = await FindDocumentAsync(studentId, docId, ct);
        if (doc == null) return Error.NotFound("Document");
        db.StudentDocuments.Remove(doc); // soft delete; the file stays on disk
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Document deleted.");
    }

    /// <summary>The document, only when its student is visible and not deleted.</summary>
    private async Task<StudentDocument?> FindDocumentAsync(int studentId, int docId, CancellationToken ct)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return null;
        return await db.StudentDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.StudentId == studentId, ct);
    }

    /// <summary>Allowed extension whose magic bytes agree with it, within the size limit; else null.</summary>
    private static async Task<string?> CheckAsync(IFormFile file, long limit, bool allowPdf, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > limit) return null;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        var allowed = allowPdf ? new[] { ".jpg", ".png", ".webp", ".pdf" } : new[] { ".jpg", ".png", ".webp" };
        if (!allowed.Contains(ext)) return null;

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header, ct);
        return FileSignature.Detect(header.AsSpan(0, read)) == ext ? ext : null;
    }
}
