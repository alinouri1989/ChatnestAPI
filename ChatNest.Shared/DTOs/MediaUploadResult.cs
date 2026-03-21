namespace ChatNest.Shared.DTOs
{
    public sealed record MediaUploadResult(Uri MediaUrl, Uri? ThumbnailUrl, long Size);

}
