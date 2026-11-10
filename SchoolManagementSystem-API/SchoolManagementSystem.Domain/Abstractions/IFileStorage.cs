namespace SchoolManagementSystem.Domain.Abstractions;

/// <summary>Where uploaded files live. Paths are relative to the storage root and never served statically.</summary>
public interface IFileStorage
{
    /// <summary>Saves the content under {campusId}/{studentId}/ with a random name; returns the relative path.</summary>
    Task<string> SaveAsync(int campusId, int studentId, string extension, Stream content, CancellationToken ct = default);

    /// <summary>Saves the content under the given relative folder (e.g. "3/candidates/12") with a random name.</summary>
    Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default);

    /// <summary>Null when the file does not exist.</summary>
    Task<Stream?> OpenAsync(string relativePath, CancellationToken ct = default);

    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}
