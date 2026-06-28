using AutoMapper;
using ChatNest.Entities.Identity;
using ChatNest.Entities.Models;
using ChatNest.Shared;
using ChatNest.Shared.DTOs;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;

namespace ChatNest.Services.Mapping
{
    /// <summary>
    /// کلاس پروفایل AutoMapper برای تبدیل اشیا نگاشت‌ها را تعریف می‌کند.
    /// </summary>
    public sealed class MappingProfile : Profile
    {
        /// <summary>
        /// یک نمونه جدید از کلاس MappingProfile می‌سازد و نگاشت‌های تبدیل بین اشیا را پیکربندی می‌کند.
        /// </summary>
        public MappingProfile()
        {
            // ✅ Converters (حل مشکل Guid <-> string و Uri <-> string)
            CreateMap<string, Guid>().ConvertUsing(src => Utility.ParseGuidOrThrow(src));

            CreateMap<Uri, string>().ConvertUsing(src => src == null ? null : src.ToString());

            CreateMap<string, Uri>().ConvertUsing(src =>
                string.IsNullOrWhiteSpace(src)
                    ? null
                    : new Uri(src, UriKind.RelativeOrAbsolute)
            );

            // SignUp => User
            CreateMap<SignUp, User>()
                .ForMember(dest => dest.Biography, opt => opt.MapFrom(_ => "سلام، من از ChatNest استفاده می‌کنم."))
                .ForMember(dest => dest.ProviderId, opt => opt.MapFrom(_ => "email"))
                .ForMember(dest => dest.LastConnectionDate, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UserSettings, opt => opt.MapFrom(_ => new UserSettings()))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserSettingsJson, opt => opt.Ignore());

            // ProviderData => User
            CreateMap<ProviderData, User>()
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Biography, opt => opt.MapFrom(_ => "سلام، من از ChatNest استفاده می‌کنم."))
                .ForMember(dest => dest.ProfilePhoto, opt => opt.MapFrom(src => new Uri(src.PhotoURL, UriKind.RelativeOrAbsolute)))
                .ForMember(dest => dest.LastConnectionDate, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.BirthDate, opt => opt.MapFrom(_ => DateTime.MinValue))
                .ForMember(dest => dest.UserSettings, opt => opt.MapFrom(_ => new UserSettings()))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore());

            // User => UserInfo (اگر UserInfo فیلد Uri/String دارد، Converter بالا مشکل را حل می‌کند)
            // User => UserInfo
            CreateMap<User, UserInfo>();


            // User => FoundUsers
            CreateMap<User, FoundUsers>();


            // User => RecipientProfile
            CreateMap<User, RecipientProfile>();


            // User => CallerUser
            CreateMap<User, CallerUser>();

            CreateMap<User, UserDto>()
                .ForMember(dest => dest.CreatedGroups, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshTokens, opt => opt.Ignore())
                .ReverseMap();
            CreateMap<Chat, ChatDto>().ReverseMap();
            CreateMap<ChatParticipant, ChatParticipantDto>()
                .ForMember(dest => dest.Chat, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore())
                .ReverseMap();
            CreateMap<Group, GroupDto>().ReverseMap();
            CreateMap<Message, MessageDto>()
                .ForMember(dest => dest.Chat, opt => opt.Ignore())
                .ReverseMap();
            CreateMap<RefreshToken, RefreshTokenDto>().ReverseMap();

            CreateMap<Group, GroupProfile>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id.ToString()))
                .ForMember(d => d.PhotoUrl, opt => opt.MapFrom(s => s.Photo))
                .ForMember(d => d.Participants, opt => opt.Ignore()); // ✅ کلیدی

            CreateMap<GroupProfile, Group>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => Guid.Parse(s.Id)))
                .ForMember(d => d.Photo, opt => opt.MapFrom(s => s.PhotoUrl))
                .ForMember(d => d.Participants, opt => opt.Ignore())      // ✅ NotMapped
                .ForMember(d => d.ParticipantsJson, opt => opt.Ignore()); // ✅ دستی ست می‌کنیم

        }
    }
}
