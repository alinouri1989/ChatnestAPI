using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ChatNest.DataAccess.Configurations;

public sealed class FileStorageConfig
{
    public FileStorageConfig(IConfiguration configuration)
    {
        var configuredRootPath = configuration["FileStorage:RootPath"] ?? Path.Combine("App_Data", "EncryptedMedia");
        RootPath = Path.IsPathRooted(configuredRootPath)
            ? configuredRootPath
            : Path.GetFullPath(configuredRootPath, AppContext.BaseDirectory);
        PublicBaseUrl = (configuration["FileStorage:PublicBaseUrl"] ?? "http://localhost:5000").TrimEnd('/');
        PublicRoutePrefix = NormalizeRoutePrefix(configuration["FileStorage:PublicRoutePrefix"] ?? "/api/media");
        EncryptionKey = ResolveEncryptionKey(configuration["FileStorage:EncryptionKey"]);
    }

    public string RootPath { get; }
    public string PublicBaseUrl { get; }
    public string PublicRoutePrefix { get; }
    public byte[] EncryptionKey { get; }

    private static string NormalizeRoutePrefix(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "/api/media";
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.TrimEnd('/');
    }

    private static byte[] ResolveEncryptionKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("FileStorage:EncryptionKey is required.");
        }

        try
        {
            var raw = Convert.FromBase64String(configuredKey);
            if (raw.Length is 16 or 24 or 32)
            {
                return raw;
            }
        }
        catch (FormatException)
        {
            // Fall back to hashing plain text secret.
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }
}
