namespace ChatNest.DataAccess.Abstract;

public sealed class StoredMediaFile(byte[] content, string contentType,
                      string? originalFileName,
                      string? thumbnailUrl = null)
{
    public byte[] Content { get; } = content;
    public string ContentType { get; } = contentType;
    public string? OriginalFileName { get; } = originalFileName;
    public string? ThumbnailUrl { get; } = thumbnailUrl;
}

