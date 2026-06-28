using ChatNest.Services.Utilities;
using Xunit;

namespace ChatNest.Tests;

public sealed class CryptoJsAesDecryptorTests
{
    [Fact]
    public void DecryptOrOriginal_DecryptsCryptoJsAesPassphrasePayload()
    {
        const string chatId = "3f1b29d2-1b7b-4d3f-a281-3d3b41f08c2a";
        const string encrypted = "U2FsdGVkX19qyZPOfFaHFBGr3JRPPkPu0OxCpW8MuEqmvERvV4GIcmzu+CR2ba2n";

        var result = CryptoJsAesDecryptor.DecryptOrOriginal(encrypted, chatId);

        Assert.Equal("سلام، چطوری؟", result);
    }

    [Fact]
    public void DecryptOrOriginal_ReturnsOriginalTextWhenPayloadIsNotCryptoJsEncrypted()
    {
        const string plainText = "New message";

        var result = CryptoJsAesDecryptor.DecryptOrOriginal(plainText, "chat-id");

        Assert.Equal(plainText, result);
    }
}
