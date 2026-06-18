using ChatNest.DataAccess.Abstract;
using ChatNest.Services.Abstract;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ChatNest.Services.Concrete;

public sealed class FirebaseNotificationService : INotificationService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<FirebaseNotificationService> _logger;
    private readonly bool _isEnabled;

    public FirebaseNotificationService(
        IConfiguration configuration,
        IUserRepository userRepository,
        ILogger<FirebaseNotificationService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
        _isEnabled = InitializeFirebase(configuration);
    }

    public async Task SendNewMessageNotificationAsync(
        string senderId,
        IEnumerable<string> recipientIds,
        string chatId,
        string chatType,
        string messagePreview,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
            return;

        var recipients = recipientIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != senderId)
            .Distinct()
            .ToList();

        if (recipients.Count == 0)
            return;

        var tokensByUser = await _userRepository.GetFcmTokensByUserIdsAsync(recipients);
        var tokens = tokensByUser.Values
            .SelectMany(userTokens => userTokens)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct()
            .ToList();

        if (tokens.Count == 0)
            return;

        var sender = await _userRepository.GetUserByIdAsync(senderId);
        var senderName = sender?.DisplayName ?? "ChatNest";
        var title = chatType.Equals("Group", StringComparison.OrdinalIgnoreCase)
            ? $"New group message from {senderName}"
            : senderName;
        var body = CreatePreview(messagePreview);

        foreach (var tokenBatch in tokens.Chunk(500))
        {
            var batchTokens = tokenBatch.ToList();
            var message = new MulticastMessage
            {
                Tokens = batchTokens,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = new Dictionary<string, string>
                {
                    ["type"] = "new_message",
                    ["chatId"] = chatId,
                    ["chatType"] = chatType,
                    ["senderId"] = senderId
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "chat_messages",
                        Sound = "default"
                    }
                },
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>
                    {
                        ["apns-priority"] = "10"
                    },
                    Aps = new Aps
                    {
                        Sound = "default",
                        ContentAvailable = true
                    }
                },
                Webpush = new WebpushConfig
                {
                    Headers = new Dictionary<string, string>
                    {
                        ["Urgency"] = "high"
                    },
                    Notification = new WebpushNotification
                    {
                        Title = title,
                        Body = body,
                        Icon = "/pwa-192.png",
                        Badge = "/pwa-192.png"
                    },
                    FcmOptions = new WebpushFcmOptions
                    {
                        Link = $"/chats/{chatId}"
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, cancellationToken);
            var invalidTokens = response.Responses
                .Select((sendResponse, index) => new { sendResponse, token = batchTokens[index] })
                .Where(item => item.sendResponse.Exception is FirebaseMessagingException exception &&
                    (exception.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                     exception.MessagingErrorCode == MessagingErrorCode.InvalidArgument))
                .Select(item => item.token)
                .ToList();

            if (invalidTokens.Count > 0)
            {
                await _userRepository.RemoveFcmTokensAsync(invalidTokens);
            }

            if (response.FailureCount > 0)
            {
                _logger.LogWarning(
                    "Firebase sent message notification with {SuccessCount} successes and {FailureCount} failures",
                    response.SuccessCount,
                    response.FailureCount);
            }
        }
    }

    private static string CreatePreview(string messagePreview)
    {
        if (string.IsNullOrWhiteSpace(messagePreview))
            return "New message";

        return messagePreview.Length <= 120
            ? messagePreview
            : $"{messagePreview[..117]}...";
    }

    private bool InitializeFirebase(IConfiguration configuration)
    {
        if (FirebaseApp.DefaultInstance != null)
            return true;

        var serviceAccountJson = configuration["Firebase:ServiceAccountJson"];
        var serviceAccountPath = configuration["Firebase:ServiceAccountPath"];
        var serviceAccountSection = configuration.GetSection("Firebase:ServiceAccount");

        try
        {
            GoogleCredential credential;
            if (!string.IsNullOrWhiteSpace(serviceAccountJson))
            {
                credential = GoogleCredential.FromJson(serviceAccountJson);
            }
            else if (serviceAccountSection.Exists())
            {
                var serviceAccount = serviceAccountSection
                    .GetChildren()
                    .ToDictionary(section => section.Key, section => section.Value ?? string.Empty);
                credential = GoogleCredential.FromJson(JsonSerializer.Serialize(serviceAccount));
            }
            else if (!string.IsNullOrWhiteSpace(serviceAccountPath))
            {
                credential = GoogleCredential.FromFile(serviceAccountPath);
            }
            else
            {
                credential = GoogleCredential.GetApplicationDefault();
            }

            FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
                ProjectId = configuration["Firebase:ProjectId"]
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase Admin was not initialized. Push notifications are disabled.");
            return false;
        }
    }
}
