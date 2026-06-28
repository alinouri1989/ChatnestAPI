using Microsoft.Extensions.Configuration;

namespace ChatNest.DataAccess.Configurations
{
    public class HuggingFaceConfig
    {        /// <summary>کلید API سرویس Hugging Face را شامل می‌شود.</summary>
        public string ApiKey { get; }



        /// <summary>آدرس API برای تولید تصویر با مدل FLUX را شامل می‌شود.</summary>
        public string FluxImage { get; }



        /// <summary>آدرس API برای تولید تصویر با مدل Artples را شامل می‌شود.</summary>
        public string ArtplesImage { get; }



        /// <summary>آدرس API برای تولید تصویر با مدل CompVis را شامل می‌شود.</summary>
        public string CompvisImage { get; }



        /// <summary>
        /// پیکربندی Hugging Face را بر اساس شیء <see cref="IConfiguration"/> مقداردهی اولیه می‌کند.
        /// </summary>
        /// <param name="configuration">شیء <see cref="IConfiguration"/> شامل تنظیمات پیکربندی برنامه.</param>
        public HuggingFaceConfig(IConfiguration configuration)
        {
            var apiKey = configuration["HuggingFace:apiKey"]!;
            var fluxImageUrl = configuration["HuggingFace:fluxImage"]!;
            var artplesImageUrl = configuration["HuggingFace:artplesImage"]!;
            var compvisImageUrl = configuration["HuggingFace:compvisImage"]!;

            ApiKey = apiKey;
            FluxImage = fluxImageUrl;
            ArtplesImage = artplesImageUrl;
            CompvisImage = compvisImageUrl;
        }
    }
}
