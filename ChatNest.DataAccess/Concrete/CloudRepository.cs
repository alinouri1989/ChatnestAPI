using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Configurations;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ChatNest.DataAccess.Concrete
{
    public sealed class CloudRepository : ICloudRepository
    {
        private readonly Cloudinary _cloudinary;

        public CloudRepository(CloudinaryConfig cloudinaryConfig)
        {
            _cloudinary = cloudinaryConfig.Cloudinary;
        }

        public async Task<Uri> UploadPhotoAsync(string publicId, string folder, string tags, MemoryStream photo)
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(publicId, photo),
                PublicId = publicId,
                Folder = folder,
                Tags = tags,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Photo upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl;
        }

        public async Task<Uri> UploadVideoAsync(string publicId, string folder, string tags, MemoryStream video)
        {
            var uploadParams = new VideoUploadParams()
            {
                File = new FileDescription(publicId, video),
                PublicId = publicId,
                Folder = folder,
                Tags = tags,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Video upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl;
        }

        public async Task<Uri> UploadAudioAsync(string publicId, string folder, string tags, MemoryStream audio)
        {
            var uploadParams = new VideoUploadParams() // Cloudinary uses VideoUploadParams for audio
            {
                File = new FileDescription(publicId, audio),
                PublicId = publicId,
                Folder = folder,
                Tags = tags,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Audio upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl;
        }

        public async Task<(Uri, long)> UploadFileAsync(string publicId, string folder, string tags, MemoryStream file)
        {
            var uploadParams = new RawUploadParams()
            {
                File = new FileDescription(publicId, file),
                PublicId = publicId,
                Folder = folder,
                Tags = tags,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"File upload failed: {uploadResult.Error.Message}");
            }

            return (uploadResult.SecureUrl, uploadResult.Bytes);
        }
    }
}