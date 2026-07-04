using ChatNest.DataAccess.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ChatNest.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public sealed class MediaController : ControllerBase
{
    private readonly IMediaStorageRepository _mediaStorageRepository;
    private static readonly TimeSpan ImmutableMediaCacheDuration = TimeSpan.FromDays(365);
    private static readonly TimeSpan MutableMediaCacheDuration = TimeSpan.FromMinutes(5);

    public MediaController(IMediaStorageRepository mediaStorageRepository)
    {
        _mediaStorageRepository = mediaStorageRepository;
    }

    [HttpGet("{folder}/{publicId}")]
    public async Task<IActionResult> Get([FromRoute] string folder, [FromRoute] string publicId)
    {
        var mediaFile = await _mediaStorageRepository.GetFileAsync(folder, publicId);
        if (mediaFile is null)
        {
            return NotFound(new { message = "Media file was not found." });
        }

        var etag = CreateETag(folder, publicId, mediaFile);
        var lastModified = new DateTimeOffset(DateTime.SpecifyKind(mediaFile.UpdatedAtUtc, DateTimeKind.Utc));

        Response.Headers.ETag = etag;
        Response.Headers.LastModified = lastModified.ToString("R");
        Response.Headers.CacheControl = IsImmutableMessageMedia(folder)
            ? $"private, max-age={(int)ImmutableMediaCacheDuration.TotalSeconds}, immutable"
            : $"private, max-age={(int)MutableMediaCacheDuration.TotalSeconds}, must-revalidate";
        Response.Headers.ContentType = mediaFile.ContentType;

        if (ClientCacheIsFresh(etag, lastModified))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        if (!string.IsNullOrWhiteSpace(mediaFile.OriginalFileName))
        {
            var contentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = mediaFile.OriginalFileName,
                FileNameStar = mediaFile.OriginalFileName
            };
            Response.Headers.ContentDisposition = contentDisposition.ToString();
        }

        return new FileContentResult(mediaFile.Content, mediaFile.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    private static bool IsImmutableMessageMedia(string folder)
    {
        return folder
            .Replace('\\', '/')
            .StartsWith("messages", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateETag(string folder, string publicId, StoredMediaFile mediaFile)
    {
        var safeFolder = folder.Replace('\\', '_').Replace('/', '_');
        return $"\"{safeFolder}-{publicId}-{mediaFile.Size}-{mediaFile.UpdatedAtUtc.Ticks}\"";
    }

    private bool ClientCacheIsFresh(string etag, DateTimeOffset lastModified)
    {
        var ifNoneMatch = Request.Headers[HeaderNames.IfNoneMatch];
        if (ifNoneMatch.Any(value => value is not null && value
                .Split(',')
                .Select(tag => tag.Trim())
                .Any(tag => tag == "*" || string.Equals(tag, etag, StringComparison.Ordinal))))
        {
            return true;
        }

        var ifModifiedSince = Request.Headers[HeaderNames.IfModifiedSince].FirstOrDefault();
        return DateTimeOffset.TryParse(ifModifiedSince, out var clientLastModified)
            && lastModified <= clientLastModified;
    }
}
