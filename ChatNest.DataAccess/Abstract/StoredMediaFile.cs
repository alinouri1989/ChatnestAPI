namespace ChatNest.DataAccess.Abstract;

public sealed record StoredMediaFile(byte[] Content, string ContentType, string? FileName);
