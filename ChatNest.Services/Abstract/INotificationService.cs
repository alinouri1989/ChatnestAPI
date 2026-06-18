namespace ChatNest.Services.Abstract;

public interface INotificationService
{
    Task SendNewMessageNotificationAsync(
        string senderId,
        IEnumerable<string> recipientIds,
        string chatId,
        string chatType,
        string messagePreview,
        CancellationToken cancellationToken = default);
}
