namespace Hmis.BuildingBlocks.Application.Files;

public sealed record StoredFile(string StoragePath, string ContentType, long SizeBytes, string Sha256);

/// <summary>
/// Centralized file storage (TAD §17). Files live outside the web root and are only served through
/// authorized endpoints. The local implementation can be replaced by S3-compatible storage later.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Stores the content under a generated name inside <paramref name="category"/> (e.g. "hospital/logo").</summary>
    Task<StoredFile> SaveAsync(Stream content, string category, string extension, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens a stored file for reading, or null if it no longer exists.</summary>
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken);
}
