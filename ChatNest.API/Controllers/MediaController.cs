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

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers.ContentType = mediaFile.ContentType;
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
}
