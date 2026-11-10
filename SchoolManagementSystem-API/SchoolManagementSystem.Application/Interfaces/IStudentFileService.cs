using Microsoft.AspNetCore.Http;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IStudentFileService
{
    Task<Result> SetPhotoAsync(int studentId, IFormFile file, CancellationToken ct = default);

    Task<Result<(Stream Content, string ContentType)>> GetPhotoAsync(int studentId, CancellationToken ct = default);

    Task<Result<StudentDocumentDto>> UploadDocumentAsync(int studentId, DocumentType type, IFormFile file, CancellationToken ct = default);

    Task<Result<(Stream Content, string ContentType, string FileName)>> GetDocumentAsync(int studentId, int docId, CancellationToken ct = default);

    Task<Result> DeleteDocumentAsync(int studentId, int docId, CancellationToken ct = default);
}
