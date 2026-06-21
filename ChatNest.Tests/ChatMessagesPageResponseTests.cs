using ChatNest.Shared.DTOs;
using Xunit;

namespace ChatNest.Tests;

public sealed class ChatMessagesPageResponseTests
{
    [Fact]
    public void EmptyResponse_HasSafeDefaults()
    {
        var response = new ChatMessagesPageResponse();

        Assert.Equal(string.Empty, response.ChatId);
        Assert.Equal(string.Empty, response.ChatType);
        Assert.Empty(response.Messages);
        Assert.False(response.HasMore);
    }
}
