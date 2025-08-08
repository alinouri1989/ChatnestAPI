using ChatNest.Entities.Enums;

namespace ChatNest.Entities.Models
{
    /// <summary>
    /// Mesaj bilgilerini temsil eden sınıf.
    /// Bir mesajın içeriği, tipi, durumu ve silindiği kullanıcılar gibi bilgileri içerir.
    /// </summary>
    public sealed class Message
    {
        public required string Content { get; set; }

        public string? FileName { get; set; }

        public long? FileSize { get; set; }

        public required MessageContent Type { get; set; }

        public required MessageStatus Status { get; set; }

        public Dictionary<string, DateTime>? DeletedFor { get; set; } = [];
    }
}