using ChatNest.Services.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChatNest.Services.Concrete;

public sealed class ConfigurableSmsOtpSender : ISmsOtpSender
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ConfigurableSmsOtpSender(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default)
    {
        var providerName = _configuration["Sms:DefaultProvider"] ?? "Melipayamak";

        ISmsOtpSender sender = providerName.Equals("Kavenegar", StringComparison.OrdinalIgnoreCase)
            ? _serviceProvider.GetRequiredService<KavenegarSmsOtpSender>()
            : _serviceProvider.GetRequiredService<MelipayamakSmsOtpSender>();

        return sender.SendOtpAsync(mobile, code, cancellationToken);
    }
}
