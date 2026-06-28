using AutoMapper;
using ChatNest.Entities.Enums;
using ChatNest.Entities.Models;
using ChatNest.Services.Mapping;
using ChatNest.Shared.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace ChatNest.Tests;

public sealed class MessagePayloadSerializationTests
{
    [Fact]
    public void MappedMessagePayload_SerializesWithoutEntityCycles()
    {
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var chat = new Chat
        {
            Id = chatId,
            ChatType = "Individual",
            CreatedDate = DateTime.UtcNow
        };

        var message = new Message
        {
            Id = messageId,
            ChatId = chatId,
            Chat = chat,
            SenderId = "user-1",
            Content = "test",
            Type = MessageContent.Text,
            CreatedDate = DateTime.UtcNow,
            Status = new MessageStatus
            {
                Sent = new Dictionary<string, DateTime> { ["user-1"] = DateTime.UtcNow }
            }
        };

        chat.Messages.Add(message);

        var payload = new Dictionary<string, Dictionary<string, Dictionary<string, MessageDto>>>
        {
            ["Individual"] = new()
            {
                [chatId.ToString()] = new()
                {
                    [messageId.ToString()] = mapper.Map<MessageDto>(message)
                }
            }
        };

        var exception = Record.Exception(() => JsonSerializer.Serialize(payload));

        Assert.Null(exception);
    }
}
