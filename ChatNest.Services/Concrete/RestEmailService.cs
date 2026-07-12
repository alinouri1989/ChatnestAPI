using ChatNest.Services.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ChatNest.Services.Concrete
{
    public sealed class RestEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RestEmailService> _logger;

        public RestEmailService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<RestEmailService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string recipient, IReadOnlyDictionary<string, string> attributes)
        {
            var endpoint = _configuration["Email:Rest:Endpoint"];
            var apiKey = _configuration["Email:Rest:ApiKey"];

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("REST email configuration is incomplete. EndpointConfigured={EndpointConfigured}, ApiKeyConfigured={ApiKeyConfigured}",
                    !string.IsNullOrWhiteSpace(endpoint), !string.IsNullOrWhiteSpace(apiKey));
                throw new InvalidOperationException("REST email settings are not configured correctly.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new { recipient, attribs = attributes })
            };
            request.Headers.Add("X-API-Key", apiKey);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("REST email request failed. StatusCode={StatusCode}, Recipient={Recipient}, Response={Response}",
                    (int)response.StatusCode, recipient, responseBody);
                response.EnsureSuccessStatusCode();
            }

            _logger.LogInformation("REST email request succeeded. Recipient={Recipient}", recipient);
        }
    }
}
