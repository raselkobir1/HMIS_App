using System.Security.Cryptography;
using Hmis.BuildingBlocks.Application.Files;
using Microsoft.Extensions.Options;

namespace Hmis.BuildingBlocks.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Root folder outside the web root. A relative path resolves against the process working directory.</summary>
    public string RootPath { get; set; } = "storage";
}

/// <summary>
/// Stores files on local or network disk under generated names. Storage paths are always relative and
/// validated to stay inside the root, so a stored path can never be used to read arbitrary files.
/// </summary>
public sealed class LocalFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string category,
        string extension,
        string contentType,
        CancellationToken cancellationToken)
    {
        var safeCategory = string.Join('/', category.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string(part.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray())));
        var safeExtension = new string(extension.TrimStart('.').Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var relativePath = $"{safeCategory}/{Guid.CreateVersion7():N}.{safeExtension}";
        var fullPath = Resolve(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var sha = SHA256.Create();
        await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var hashing = new CryptoStream(file, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashing, cancellationToken);
        }

        var size = new FileInfo(fullPath).Length;
        return new StoredFile(relativePath, contentType, size, Convert.ToHexString(sha.Hash!));
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = Resolve(storagePath);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = Resolve(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path escapes the storage root.");
        }

        return fullPath;
    }
}
