using System.Net.Http.Json;
using ChatNest.Services.Abstract;
using Microsoft.Extensions.Configuration;

namespace ChatNest.Services.Concrete;

public sealed class MelipayamakSmsOtpSender : ISmsOtpSender
{
    private const string BaseServiceNumberUrl = "https://rest.payamak-panel.com/api/SendSMS/BaseServiceNumber";
    private const string SharedEndpointBaseUrl = "https://console.melipayamak.com/api/send/shared";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MelipayamakSmsOtpSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default)
    {
        var sendMode = _configuration["Melipayamak:SendMode"] ?? "Shared";
        if (sendMode.Equals("BaseServiceNumber", StringComparison.OrdinalIgnoreCase))
        {
            await SendByBaseServiceNumberAsync(mobile, code, cancellationToken);
            return;
        }

        await SendBySharedEndpointAsync(mobile, code, cancellationToken);
    }

    private async Task SendBySharedEndpointAsync(string mobile, string code, CancellationToken cancellationToken)
    {
        var ApiKey = _configuration["Melipayamak:ApiKey"];
        var bodyId = _configuration.GetValue<int?>("Melipayamak:BodyId");

        if (string.IsNullOrWhiteSpace(ApiKey) || bodyId is null or <= 0)
        {
            throw new InvalidOperationException("Melipayamak shared token or bodyId is not configured.");
        }

        using var response = await _httpClient.PostAsJsonAsync(
            $"{SharedEndpointBaseUrl}/{ApiKey}",
            new
            {
                bodyId = bodyId.Value,
                to = NormalizeIranMobileForMelipayamak(mobile),
                args = new[] { code }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task SendByBaseServiceNumberAsync(string mobile, string code, CancellationToken cancellationToken)
    {
        var username = _configuration["Melipayamak:Username"];
        var password = _configuration["Melipayamak:Password"];
        var bodyId = _configuration.GetValue<int?>("Melipayamak:BodyId");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || bodyId is null or <= 0)
        {
            throw new InvalidOperationException("Melipayamak username, password, or bodyId is not configured.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["text"] = code,
            ["to"] = NormalizeIranMobileForMelipayamak(mobile),
            ["bodyId"] = bodyId.Value.ToString()
        });

        using var response = await _httpClient.PostAsync(BaseServiceNumberUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MelipayamakResponse>(cancellationToken);
        if (result == null || !IsSuccessful(result.RetStatus, result.Value))
        {
            throw new InvalidOperationException($"Melipayamak OTP send failed. RetStatus={result?.RetStatus}, Value={result?.Value}");
        }
    }

    private static string NormalizeIranMobileForMelipayamak(string mobile)
    {
        if (mobile.StartsWith("+98", StringComparison.Ordinal) && mobile.Length == 13)
        {
            return "0" + mobile[3..];
        }

        if (mobile.StartsWith("98", StringComparison.Ordinal) && mobile.Length == 12)
        {
            return "0" + mobile[2..];
        }

        return mobile;
    }

    private static bool IsSuccessful(int retStatus, string? value)
    {
        if (retStatus == 1)
        {
            return true;
        }

        return long.TryParse(value, out var recId) && recId > 0;
    }

    private sealed record MelipayamakResponse(int RetStatus, string? Value, string? StrRetStatus);
}
