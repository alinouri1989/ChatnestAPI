using Microsoft.Extensions.Configuration;

namespace ChatNest.DataAccess.Configurations
{
    public class GeminiConfig
    {

        public GeminiConfig(IConfiguration configuration)
        {
            var apiKey = configuration["Gemini:apiKey"];
            var textUrl = configuration["Gemini:textUrl"];

            TextGeneration = textUrl + apiKey;
        }
        public string ApiKey { get; set; } = string.Empty;
        public string TextGeneration { get; set; } = string.Empty;
    }
}