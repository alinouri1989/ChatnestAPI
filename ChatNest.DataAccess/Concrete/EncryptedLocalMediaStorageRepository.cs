using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Configurations;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChatNest.DataAccess.Concrete;

public sealed class EncryptedLocalMediaStorageRepository : IMediaStorageRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FileStorageConfig _fileStorageConfig;

    public EncryptedLocalMediaStorageRepository(FileStorageConfig fileStorageConfig)
    {
        _fileStorageConfig = fileStorageConfig;
        Directory.CreateDirectory(_fileStorageConfig.RootPath);
    }

    public Task<Uri> UploadPhotoAsync(string publicId, string folder, string tags, MemoryStream photo, string? originalFileName = null) =>
        UploadBinaryAsync(publicId, folder, photo, "image/jpeg", originalFileName);

    public Task<Uri> UploadVideoAsync(string publicId, string folder, string tags, MemoryStream video, string? originalFileName = null) =>
        UploadBinaryAsync(publicId, folder, video, "video/mp4", originalFileName);

    public Task<Uri> UploadAudioAsync(string publicId, string folder, string tags, MemoryStream audio, string? originalFileName = null) =>
        UploadBinaryAsync(publicId, folder, audio, "audio/mpeg", originalFileName);

    public async Task<(Uri Url, long Size)> UploadFileAsync(string publicId, string folder, string tags, MemoryStream file, string? originalFileName = null)
    {
        var bytes = file.ToArray();
        await PersistEncryptedAsync(publicId, folder, bytes, originalFileName, "application/octet-stream");
        return (BuildPublicUri(folder, publicId), bytes.LongLength);
    }

    public async Task<StoredMediaFile?> GetFileAsync(string folder, string publicId)
    {
        var paths = GetPaths(folder, publicId);
        if (!File.Exists(paths.DataPath) || !File.Exists(paths.MetadataPath))
        {
            return null;
        }

        var metadataJson = await File.ReadAllTextAsync(paths.MetadataPath);
        var metadata = JsonSerializer.Deserialize<StoredMediaMetadata>(metadataJson, JsonOptions);
        if (metadata is null)
        {
            throw new InvalidOperationException("Stored media metadata is invalid.");
        }

        await using var fileStream = new FileStream(paths.DataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var iv = new byte[16];
        var readIv = await fileStream.ReadAsync(iv);
        if (readIv != iv.Length)
        {
            throw new InvalidOperationException("Encrypted media file is corrupted.");
        }

        using var aes = Aes.Create();
        aes.Key = _fileStorageConfig.EncryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var cryptoStream = new CryptoStream(fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var memoryStream = new MemoryStream();
        await cryptoStream.CopyToAsync(memoryStream);

        return new StoredMediaFile(memoryStream.ToArray(), metadata.ContentType, metadata.OriginalFileName);
    }

    private async Task<Uri> UploadBinaryAsync(string publicId, string folder, MemoryStream stream, string defaultContentType, string? originalFileName)
    {
        var bytes = stream.ToArray();
        await PersistEncryptedAsync(publicId, folder, bytes, originalFileName, defaultContentType);
        return BuildPublicUri(folder, publicId);
    }

    private async Task PersistEncryptedAsync(string publicId, string folder, byte[] plainBytes, string? originalFileName, string defaultContentType)
    {
        var paths = GetPaths(folder, publicId);
        var contentType = ResolveContentType(plainBytes, originalFileName, defaultContentType);

        var metadata = new StoredMediaMetadata
        {
            ContentType = contentType,
            OriginalFileName = originalFileName,
            Size = plainBytes.LongLength,
            UpdatedAtUtc = DateTime.UtcNow
        };

        using var aes = Aes.Create();
        aes.Key = _fileStorageConfig.EncryptionKey;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using (var fileStream = new FileStream(paths.DataPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await fileStream.WriteAsync(aes.IV);
            await using var cryptoStream = new CryptoStream(fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: false);
            await cryptoStream.WriteAsync(plainBytes);
            cryptoStream.FlushFinalBlock();
        }

        var metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(paths.MetadataPath, metadataJson);
    }

    private Uri BuildPublicUri(string folder, string publicId)
    {
        var escapedFolder = Uri.EscapeDataString(folder);
        var escapedId = Uri.EscapeDataString(publicId);
        return new Uri($"{_fileStorageConfig.PublicBaseUrl}{_fileStorageConfig.PublicRoutePrefix}/{escapedFolder}/{escapedId}");
    }

    private (string DataPath, string MetadataPath) GetPaths(string folder, string publicId)
    {
        var safeFolder = SanitizeSegment(folder);
        var safePublicId = SanitizeSegment(publicId);
        var folderPath = Path.Combine(_fileStorageConfig.RootPath, safeFolder);
        Directory.CreateDirectory(folderPath);

        return (
            Path.Combine(folderPath, $"{safePublicId}.bin"),
            Path.Combine(folderPath, $"{safePublicId}.json")
        );
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Media path segment cannot be empty.", nameof(value));
        }

        var sanitizedChars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_')
            .ToArray();

        var sanitized = new string(sanitizedChars).Trim('.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Media path segment is invalid.", nameof(value));
        }

        return sanitized;
    }

    private static string ResolveContentType(byte[] bytes, string? originalFileName, string defaultContentType)
    {
        if (!string.IsNullOrWhiteSpace(originalFileName))
        {
            var byExtension = GetContentTypeFromExtension(Path.GetExtension(originalFileName));
            if (!string.IsNullOrWhiteSpace(byExtension))
            {
                return byExtension;
            }
        }

        var bySignature = GetContentTypeFromSignature(bytes);
        return bySignature ?? defaultContentType;
    }

    private static string? GetContentTypeFromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            _ => null
        };
    }

    private static string? GetContentTypeFromSignature(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 6 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
            bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        if (bytes.Length >= 12 &&
            bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70)
        {
            return "video/mp4";
        }

        if (bytes.Length >= 3 && bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33)
        {
            return "audio/mpeg";
        }

        if (bytes.Length >= 4 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
        {
            return "audio/wav";
        }

        if (bytes.Length >= 4 &&
            bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
        {
            return "application/pdf";
        }

        return null;
    }

    private sealed class StoredMediaMetadata
    {
        public required string ContentType { get; init; }
        public string? OriginalFileName { get; init; }
        public long Size { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
