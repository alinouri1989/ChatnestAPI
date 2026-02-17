using AutoMapper;
using ChatNest.Entities.Models;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using User = ChatNest.Entities.Models.User;
using UserInfo = ChatNest.Shared.DTOs.Response.UserInfo;

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
            // SignUp => User
            CreateMap<SignUp, User>()
                .ForMember(dest => dest.Biography, opt => opt.MapFrom(src => "سلام، من از ChatNest استفاده می‌کنم."))
                .ForMember(dest => dest.ProfilePhoto, opt => opt.MapFrom(src => "https://res.cloudinary.com/ChatNest-realtime-messaging-app/image/upload/v1744980054/DefaultUserProfilePhoto.png"))
                .ForMember(dest => dest.ProviderId, opt => opt.MapFrom(src => "email"))
                .ForMember(dest => dest.LastConnectionDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UserSettings, opt => opt.MapFrom(src => new UserSettings()))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserSettingsJson, opt => opt.Ignore());


            // ProviderData => User
            CreateMap<ProviderData, User>()
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.Biography, opt => opt.MapFrom(src => "سلام، من از ChatNest استفاده می‌کنم."))
                .ForMember(dest => dest.ProfilePhoto, opt => opt.MapFrom(src => new Uri(src.PhotoURL)))
                .ForMember(dest => dest.LastConnectionDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.BirthDate, opt => opt.MapFrom(src => DateTime.MinValue))
                .ForMember(dest => dest.UserSettings, opt => opt.MapFrom(src => new UserSettings()))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore());


            // User => UserInfo
            CreateMap<User, UserInfo>();


            // User => FoundUsers
            CreateMap<User, FoundUsers>();


            // User => RecipientProfile
            CreateMap<User, RecipientProfile>();


            // User => CallerUser
            CreateMap<User, CallerUser>();

            // Group mappings
            CreateMap<Group, GroupProfile>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.PhotoUrl, opt => opt.MapFrom(src => src.Photo != null ? src.Photo.ToString() : null));
        }
    }
}
