namespace ChatNest.DataAccess.Abstract;

public interface IMediaStorageRepository
{
    Task<Uri> UploadPhotoAsync(string publicId, string folder, string tags, MemoryStream photo, string? originalFileName = null);
    Task<Uri> UploadVideoAsync(string publicId, string folder, string tags, MemoryStream video, string? originalFileName = null);
    Task<Uri> UploadAudioAsync(string publicId, string folder, string tags, MemoryStream audio, string? originalFileName = null);
    Task<(Uri Url, long Size)> UploadFileAsync(string publicId, string folder, string tags, MemoryStream file, string? originalFileName = null);
    Task<StoredMediaFile?> GetFileAsync(string folder, string publicId);
}
