using ChatNest.Services.Abstract;
using Kavenegar;
using Microsoft.Extensions.Configuration;

namespace ChatNest.Services.Concrete;

public sealed class KavenegarSmsOtpSender : ISmsOtpSender
{
    private readonly IConfiguration _configuration;

    public KavenegarSmsOtpSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Kavenegar:ApiKey"];
        var template = _configuration["Kavenegar:OtpTemplate"] ?? "verify";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Kavenegar API key is not configured.");
        }

        var api = new KavenegarApi(apiKey);
        return Task.Run(() => api.VerifyLookup(mobile, code, template), cancellationToken);
    }
}
