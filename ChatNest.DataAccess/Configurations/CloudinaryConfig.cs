using CloudinaryDotNet;
using Microsoft.Extensions.Configuration;

namespace ChatNest.DataAccess.Configurations
{

    public class CloudinaryConfig
    {
        public CloudinaryConfig(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:cloudName"];
            var apiKey = configuration["Cloudinary:apiKey"];
            var apiSecret = configuration["Cloudinary:apiSecret"];

            Account account = new Account(
                cloudName,
                apiKey,
                apiSecret
            );

            Cloudinary = new Cloudinary(account)
            {
                Api = { Secure = true }
            };
        }
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public CloudinaryDotNet.Cloudinary Cloudinary { get; set; } = null!;
    }
}