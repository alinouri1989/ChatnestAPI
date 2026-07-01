using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Configurations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;
using System.Text.Json;
using Xabe.FFmpeg;

namespace ChatNest.DataAccess.Concrete;

public sealed class EncryptedLocalMediaStorageRepository : IMediaStorageRepository
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly FileStorageConfig _fileStorageConfig;

    public EncryptedLocalMediaStorageRepository(FileStorageConfig fileStorageConfig)
    {
        _fileStorageConfig = fileStorageConfig;
        Directory.CreateDirectory(_fileStorageConfig.RootPath);
    }

    #region ----------- Public Upload APIs ---------------------------------

    public Task<Uri> UploadPhotoAsync(string publicId,
                                      string folder,
                                      string tags,
                                      MemoryStream photo,
                                      string? originalFileName = null) =>
        UploadBinaryAsync(publicId, folder, photo, "image/jpeg", originalFileName);

    public Task<Uri> UploadVideoAsync(string publicId,
                                      string folder,
                                      string tags,
                                      MemoryStream video,
                                      string? originalFileName = null) =>
        UploadBinaryAsync(publicId, folder, video, "video/mp4", originalFileName);

    public Task<Uri> UploadAudioAsync(string publicId,
                                      string folder,
                                      string tags,
                                      MemoryStream audio,
                                      string? originalFileName = null) =>
        UploadBinaryAsync(publicId, folder, audio, "audio/mpeg", originalFileName);

    public async Task<(Uri Url, long Size)> UploadFileAsync(string publicId,
                                                            string folder,
                                                            string tags,
                                                            MemoryStream file,
                                                            string? originalFileName = null)
    {
        var bytes = file.ToArray();
        var uri = await PersistEncryptedAsync(publicId,
                                               folder,
                                               bytes,
                                               originalFileName,
                                               "application/octet-stream");
        return (uri.Item1, bytes.LongLength);
    }

    public async Task<(Uri?, Uri?)> UploadPhotoWithThumbnailAsync(
    string publicId,
    string folder,
    string tags,
    MemoryStream photo,
    string? originalFileName = null,
    int thumbWidth = 250,          // عرض پیش‌فرض thumbnail
    int thumbHeight = 250)         // ارتفاع پیش‌فرض (اگر 0 باشد نسبت‌مند می‌شود)
    {
        var originalBytes = photo.ToArray();

        // ---------- 1️⃣ ساخت thumbnail ----------
        photo.Position = 0;                     // بازنشانی استریم
        var thumbBytes = await CreateImageThumbnailAsync(photo, thumbWidth, thumbHeight);

        // ---------- 2️⃣ ذخیرهٔ thumbnail ----------
        var thumbFolder = Path.Combine(folder, "thumbnails");
        var thumbId = $"{publicId}_thumb";

        await PersistEncryptedAsync(
            thumbId,
            thumbFolder,
            thumbBytes,
            $"{publicId}_thumb.jpg",
            "image/jpeg");

        // ---------- 3️⃣ ذخیرهٔ تصویر اصلی با URI thumbnail ----------
        var uris = await PersistEncryptedAsync(
            publicId,
            folder,
            originalBytes,
            originalFileName,
            "image/jpeg",
            thumbFolder,
            thumbId);

        // برگرداندن (uri تصویر اصلی , uri thumbnail)
        return (uris.Item1, uris.Item2);
    }
    private async Task<byte[]> CreateImageThumbnailAsync(
        Stream imageStream,
        int width,
        int height)
    {
        // ImageSharp به صورت async کار نمی‌کند، پس از MemoryStream استفاده می‌کنیم
        using var image = await Image.LoadAsync(imageStream);

        // اگر فقط عرض یا فقط ارتفاع داده شده باشد، نسبت حفظ می‌شود
        if (width > 0 && height > 0)
            image.Mutate(x => x.Resize(width, height));
        else if (width > 0)
            image.Mutate(x => x.Resize(width, 0));
        else if (height > 0)
            image.Mutate(x => x.Resize(0, height));
        else
            throw new ArgumentException("حداقل یکی از عرض یا ارتفاع باید بزرگتر از صفر باشد.");

        // ذخیره به فرمت JPEG (می‌توانید quality را هم تنظیم کنید)
        var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
        {
            Quality = 65   // کیفیت مناسب – می‌توانید تغییر دهید
        });
        return ms.ToArray();
    }


    /// <summary>
    /// آپلود ویدیو به همراه ساخت thumbnail.
    /// مسیر thumbnail داخل متادیتای ویدیو (فیلد ThumbnailUri) ذخیره می‌شود.
    /// </summary>
    public async Task<(Uri?, Uri?)> UploadVideoWithThumbnailAsync(string publicId,
                                                         string folder,
                                                         string tags,
                                                         MemoryStream video,
                                                         string? originalFileName = null)
    {
        var videoBytes = video.ToArray();
        string? thumbFolder = null;
        string? thumbId = null;

        try
        {
            video.Position = 0;
            var thumbBytes = await CreateThumbnailAsync(video);
            thumbFolder = Path.Combine(folder, "thumbnails");
            thumbId = $"{publicId}_thumb";

            await PersistEncryptedAsync(thumbId,
                                        thumbFolder,
                                        thumbBytes,
                                        $"{publicId}_thumb.jpg",
                                        "image/jpeg");
        }
        catch
        {
            thumbFolder = null;
            thumbId = null;
        }

        return await PersistEncryptedAsync(publicId,
                                           folder,
                                           videoBytes,
                                           originalFileName,
                                           "video/mp4",
                                           thumbFolder,
                                           thumbId);
    }

    #endregion

    #region ----------- Private Helpers ------------------------------------

    private async Task<Uri> UploadBinaryAsync(string publicId,
                                              string folder,
                                              MemoryStream stream,
                                              string defaultContentType,
                                              string? originalFileName)
    {
        var bytes = stream.ToArray();
        var uri = await PersistEncryptedAsync(publicId,
                                            folder,
                                            bytes,
                                            originalFileName,
                                            defaultContentType);
        return uri.Item1;
    }

    /// <summary>
    /// ذخیرهٔ داده‌ها به‌صورت رمزنگاری‌شده، نوشتن متادیتا
    /// و (در صورت ارائه) پر کردن فیلد ThumbnailUri.
    /// در صورت موفقیت، URI عمومی مورد ذخیره‌سازی برگردانده می‌شود.
    /// </summary>
    private async Task<(Uri?, Uri?)> PersistEncryptedAsync(string publicId,
                                                  string folder,
                                                  byte[] plainBytes,
                                                  string? originalFileName,
                                                  string defaultContentType,
                                                  string? thumbFolder = null,
                                                  string? thumbId = null)
    {
        var paths = GetPaths(folder, publicId);
        var contentType = ResolveContentType(plainBytes, originalFileName, defaultContentType);

        var metadata = new StoredMediaMetadata
        {
            ContentType = contentType,
            OriginalFileName = originalFileName,
            Size = plainBytes.LongLength,
            UpdatedAtUtc = DateTime.UtcNow,

            // اگر مسیر thumbnail داده شد، در اینجا قرار می‌گیرد
            ThumbnailUri = (thumbFolder != null && thumbId != null)
                           ? BuildPublicUri(thumbFolder, thumbId)
                           : null
        };

        // ---------- رمزنگاری و نوشتن فایل ----------
        using var aes = Aes.Create();
        aes.Key = _fileStorageConfig.EncryptionKey;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using (var fileStream = new FileStream(paths.DataPath,
                                                    FileMode.Create,
                                                    FileAccess.Write,
                                                    FileShare.None,
                                                    81920,
                                                    useAsync: true))
        {
            // ابتدا IV را می‌نویسیم
            await fileStream.WriteAsync(aes.IV);
            await using var cryptoStream = new CryptoStream(fileStream,
                                                            aes.CreateEncryptor(),
                                                            CryptoStreamMode.Write,
                                                            leaveOpen: false);
            await cryptoStream.WriteAsync(plainBytes);
            cryptoStream.FlushFinalBlock();
        }

        // ---------- نوشتن متادیتا ----------
        var metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(paths.MetadataPath, metadataJson);

        // بازگشت مسیر عمومی (URI) برای فراخوانی‌کننده
        return new(BuildPublicUri(folder, publicId), metadata.ThumbnailUri);
    }

    private async Task<byte[]> CreateThumbnailAsync(Stream videoStream)
    {
        // ذخیرهٔ موقت ویدیو
        var tmpVideo = Path.GetTempFileName();
        await using (var file = new FileStream(tmpVideo, FileMode.Create, FileAccess.Write))
            await videoStream.CopyToAsync(file);

        // مسیر تصویر کوچک موقت
        var tmpImg = Path.GetTempFileName() + ".jpg";

        // استخراج فریم صفر ثانیه (یا زمان دلخواه)
        var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(tmpVideo, tmpImg, TimeSpan.FromSeconds(0));
        await conversion.Start();

        var bytes = await File.ReadAllBytesAsync(tmpImg);

        // حذف فایل‌های موقت
        File.Delete(tmpVideo);
        File.Delete(tmpImg);

        return bytes;
    }

    public async Task<StoredMediaFile?> GetFileAsync(string folder, string publicId)
    {
        var paths = GetPaths(folder, publicId);
        if (!File.Exists(paths.DataPath) || !File.Exists(paths.MetadataPath))
            return null;

        var metadataJson = await File.ReadAllTextAsync(paths.MetadataPath);
        var metadata = JsonSerializer.Deserialize<StoredMediaMetadata>(metadataJson, JsonOptions);
        if (metadata is null)
            throw new InvalidOperationException("Stored media metadata is invalid.");

        await using var fileStream = new FileStream(paths.DataPath,
                                                    FileMode.Open,
                                                    FileAccess.Read,
                                                    FileShare.Read,
                                                    81920,
                                                    useAsync: true);
        var iv = new byte[16];
        var readIv = await fileStream.ReadAsync(iv);
        if (readIv != iv.Length)
            throw new InvalidOperationException("Encrypted media file is corrupted.");

        using var aes = Aes.Create();
        aes.Key = _fileStorageConfig.EncryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using var cryptoStream = new CryptoStream(fileStream,
                                                        aes.CreateDecryptor(),
                                                        CryptoStreamMode.Read);
        await using var memoryStream = new MemoryStream();
        await cryptoStream.CopyToAsync(memoryStream);

        return new StoredMediaFile(
            memoryStream.ToArray(),
            metadata.ContentType,
            metadata.OriginalFileName,
            metadata.ThumbnailUri?.ToString()
        );
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
            throw new ArgumentException("Media path segment cannot be empty.", nameof(value));

        var sanitizedChars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_')
            .ToArray();

        var sanitized = new string(sanitizedChars).Trim('.');

        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Media path segment is invalid.", nameof(value));

        return sanitized;
    }

    private static string ResolveContentType(byte[] bytes,
                                              string? originalFileName,
                                              string defaultContentType)
    {
        if (!string.IsNullOrWhiteSpace(originalFileName))
        {
            var byExt = GetContentTypeFromExtension(Path.GetExtension(originalFileName));
            if (!string.IsNullOrWhiteSpace(byExt))
                return byExt;
        }

        var bySig = GetContentTypeFromSignature(bytes);
        return bySig ?? defaultContentType;
    }

    private static string? GetContentTypeFromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".apk" => "application/vnd.android.package-archive",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
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
        // JPEG
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // PNG
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        // GIF
        if (bytes.Length >= 6 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
            bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61)
            return "image/gif";

        // WebP
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        // MP4
        if (bytes.Length >= 12 &&
            bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70)
            return "video/mp4";

        // MP3 (ID3)
        if (bytes.Length >= 3 && bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33)
            return "audio/mpeg";

        // WAV (RIFF … WAVE)
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x41 && bytes[10] == 0x56 && bytes[11] == 0x45)
            return "audio/wav";

        // PDF
        if (bytes.Length >= 4 &&
            bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
            return "application/pdf";

        // ZIP (PK..)
        if (bytes.Length >= 4 &&
            bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04)
            return "application/zip";

        // RAR (older)
        if (bytes.Length >= 7 &&
            bytes[0] == 0x52 && bytes[1] == 0x61 && bytes[2] == 0x72 && bytes[3] == 0x21 &&
            bytes[4] == 0x1A && bytes[5] == 0x07 && bytes[6] == 0x00)
            return "application/vnd.rar";

        // اگر هیچ کدام مطابقت نداشت
        return null;
    }

    private sealed class StoredMediaMetadata
    {
        public required string ContentType { get; init; }
        public string? OriginalFileName { get; init; }
        public long Size { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public Uri? ThumbnailUri { get; init; }
    }

}
#endregion
