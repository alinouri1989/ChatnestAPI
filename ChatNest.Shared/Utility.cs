using AutoMapper;

namespace ChatNest.Shared
{
    public static class Utility
    {
        public static Guid ParseGuidOrThrow(string src)
        {
            if (Guid.TryParse(src, out var g)) return g;
            throw new AutoMapperMappingException($"Invalid Guid value: '{src}'");
        }
    }
}
