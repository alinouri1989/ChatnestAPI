using System.Security.Cryptography;
using System.Text;

namespace ChatNest.Services.Utilities;

public static class CryptoJsAesDecryptor
{
    private const string SaltedPrefix = "Salted__";
    private const int SaltLength = 8;
    private const int KeyLength = 32;
    private const int IvLength = 16;

    public static string DecryptOrOriginal(string? encryptedContent, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(encryptedContent) || string.IsNullOrEmpty(passphrase))
            return encryptedContent ?? string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedContent);
            var prefixBytes = Encoding.ASCII.GetBytes(SaltedPrefix);
            if (encryptedBytes.Length <= prefixBytes.Length + SaltLength ||
                !encryptedBytes.AsSpan(0, prefixBytes.Length).SequenceEqual(prefixBytes))
            {
                return encryptedContent;
            }

            var salt = encryptedBytes.AsSpan(prefixBytes.Length, SaltLength).ToArray();
            var cipherText = encryptedBytes.AsSpan(prefixBytes.Length + SaltLength).ToArray();
            var (key, iv) = DeriveKeyAndIv(passphrase, salt);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            var decrypted = Encoding.UTF8.GetString(plainBytes);

            return string.IsNullOrEmpty(decrypted) ? encryptedContent : decrypted;
        }
        catch
        {
            return encryptedContent;
        }
    }

    private static (byte[] Key, byte[] Iv) DeriveKeyAndIv(string passphrase, byte[] salt)
    {
        var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        var keyAndIv = new byte[KeyLength + IvLength];
        var generatedBytes = 0;
        byte[] previous = [];

        while (generatedBytes < keyAndIv.Length)
        {
            var input = new byte[previous.Length + passphraseBytes.Length + salt.Length];
            Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
            Buffer.BlockCopy(passphraseBytes, 0, input, previous.Length, passphraseBytes.Length);
            Buffer.BlockCopy(salt, 0, input, previous.Length + passphraseBytes.Length, salt.Length);

            previous = MD5.HashData(input);
            var bytesToCopy = Math.Min(previous.Length, keyAndIv.Length - generatedBytes);
            Buffer.BlockCopy(previous, 0, keyAndIv, generatedBytes, bytesToCopy);
            generatedBytes += bytesToCopy;
        }

        var key = keyAndIv[..KeyLength];
        var iv = keyAndIv[KeyLength..];
        return (key, iv);
    }
}
