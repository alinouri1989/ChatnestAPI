using ChatNest.DataAccess.Configurations;
using ChatNest.Services.Abstract;
using ChatNest.Shared.DTOs.Request;
using System.Text;
using System.Text.Json;

namespace ChatNest.Services.Concrete
{
    public sealed class GenerativeAiService : IGenerativeAiService
    {
        private readonly GeminiConfig _geminiConfig;
        private readonly HuggingFaceConfig _huggingFaceConfig;
        private readonly HttpClient _httpClient;

        public GenerativeAiService(GeminiConfig geminiConfig, HuggingFaceConfig huggingFaceConfig, HttpClient httpClient)
        {
            _geminiConfig = geminiConfig;
            _huggingFaceConfig = huggingFaceConfig;
            _httpClient = httpClient;
        }

        public async Task<string> GeminiGenerateTextAsync(AiRequest request)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = request.Prompt }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var url = $"{_geminiConfig.TextGeneration}?key={_geminiConfig.ApiKey}";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonDocument.Parse(responseContent);

                // Extract the generated text from Gemini response
                var candidates = jsonResponse.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    var contentProp = firstCandidate.GetProperty("content");
                    var parts = contentProp.GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString() ?? "No response generated";
                    }
                }
            }

            return "Failed to generate response";
        }

        public async Task<string> HfGenerateImageAsync(AiRequest request)
        {
            var requestBody = new { inputs = request.Prompt };
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Set the authorization header
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_huggingFaceConfig.ApiKey}");

            // Use different models based on request type or preferences
            var modelUrl = request.Model switch
            {
                "flux" => _huggingFaceConfig.FluxImage,
                "artples" => _huggingFaceConfig.ArtplesImage,
                "compvis" => _huggingFaceConfig.CompvisImage,
                _ => _huggingFaceConfig.FluxImage
            };

            var response = await _httpClient.PostAsync(modelUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseBytes = await response.Content.ReadAsByteArrayAsync();

                // Convert image bytes to base64 string for transmission
                var base64String = Convert.ToBase64String(responseBytes);
                return $"data:image/png;base64,{base64String}";
            }

            return "Failed to generate image";
        }
    }
}