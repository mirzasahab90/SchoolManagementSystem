using SchoolManagementSystem.Domain.Abstractions;

namespace SchoolManagementSystem.Api.Infrastructure;

/// <summary>
/// Stores uploads on local disk under Storage:UploadsPath (default App_Data/uploads under the content root).
/// Refuses any path that would escape the root.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Storage:UploadsPath"];
        var path = string.IsNullOrWhiteSpace(configured) ? Path.Combine("App_Data", "uploads") : configured;
        _root = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path));
        Directory.CreateDirectory(_root);
    }

    public Task<string> SaveAsync(int campusId, int studentId, string extension, Stream content, CancellationToken ct = default) =>
        SaveAsync($"{campusId}/{studentId}", extension, content, ct);

    public async Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default)
    {
        var relative = Path.Combine(folder.Replace('/', Path.DirectorySeparatorChar), $"{Guid.NewGuid():N}{extension}");
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = new FileStream(full, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
        return relative.Replace('\\', '/');
    }

    public Task<Stream?> OpenAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        return Task.FromResult<Stream?>(File.Exists(full) ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage path.");
        return full;
    }
}
