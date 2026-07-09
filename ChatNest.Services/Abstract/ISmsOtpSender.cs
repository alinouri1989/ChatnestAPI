namespace ChatNest.Services.Abstract;

public interface ISmsOtpSender
{
    Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default);
}
