using ChatNest.Services.Exceptions;

namespace ChatNest.Services.Utilities
{
    /// <summary>
    /// کلاس کمکی برای اعتبارسنجی فایل‌ها.
    /// </summary>
    internal static class FileValidationHelper
    {
        /// <summary>
        /// فایل عکس را اعتبارسنجی می‌کند. اگر حجم فایل بیشتر از 2 مگابایت باشد، BadRequestException پرتاب می‌کند.
        /// </summary>
        /// <param name="base64File">داده عکس در قالب Base64</param>
        /// <returns>جریان حافظه عکس را برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">اگر حجم عکس بیشتر از 2 مگابایت باشد، استثنا پرتاب می‌شود.</exception>
        public static MemoryStream ValidatePhoto(string base64File)
        {
            var photoBytes = Convert.FromBase64String(base64File);
            var photo = new MemoryStream(photoBytes);

            int maxFileSize = 2 * 1024 * 1024;

            if (photo.Length > maxFileSize)
            {
                throw new BadRequestException($"حجم عکس باید حداکثر 2 مگابایت باشد.");
            }

            return photo;
        }



        /// <summary>
        /// فایل ویدیو را اعتبارسنجی می‌کند. اگر حجم فایل بیشتر از 100 مگابایت باشد، BadRequestException پرتاب می‌کند.
        /// </summary>
        /// <param name="base64File">داده ویدیو در قالب Base64</param>
        /// <returns>جریان حافظه ویدیو را برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">اگر حجم ویدیو بیشتر از 100 مگابایت باشد، استثنا پرتاب می‌شود.</exception>
        public static MemoryStream ValidateVideo(string base64File)
        {
            var videoBytes = Convert.FromBase64String(base64File);
            var video = new MemoryStream(videoBytes);

            int maxFileSize = 100 * 1024 * 1024;

            if (video.Length > maxFileSize)
            {
                throw new BadRequestException($"حجم ویدیو باید حداکثر 100 مگابایت باشد.");
            }

            return video;
        }



        /// <summary>
        /// اعتبارسنجی عمومی فایل را انجام می‌دهد. اگر حجم فایل بیشتر از 200 مگابایت باشد، BadRequestException پرتاب می‌کند.
        /// </summary>
        /// <param name="base64File">داده فایل در قالب Base64</param>
        /// <returns>جریان حافظه فایل را برمی‌گرداند.</returns>
        /// <exception cref="BadRequestException">اگر حجم فایل بیشتر از 200 مگابایت باشد، استثنا پرتاب می‌شود.</exception>
        public static MemoryStream ValidateFile(string base64File)
        {
            var fileBytes = Convert.FromBase64String(base64File);
            var file = new MemoryStream(fileBytes);

            int maxFileSize = 200 * 1024 * 1024;

            if (file.Length > maxFileSize)
            {
                throw new BadRequestException($"حجم فایل باید حداکثر 200 مگابایت باشد.");
            }

            return file;
        }
    }
}
