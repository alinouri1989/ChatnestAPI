namespace ChatNest.Services.Abstract
{
    public interface IEmailService
    {
        Task SendEmailAsync(string recipient, IReadOnlyDictionary<string, string> attributes);
    }
}
