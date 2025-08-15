using AutoMapper;
using ChatNest.Entities.Models;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using User = ChatNest.Entities.Models.User;
using UserInfo = ChatNest.Shared.DTOs.Response.UserInfo;

namespace ChatNest.Services.Mapping
{
    /// <summary>
    /// AutoMapper profil sınıfı, nesne dönüştürme işlemleri için harita tanımlar.
    /// </summary>
    public sealed class MappingProfile : Profile
    {
        /// <summary>
        /// MappingProfile sınıfının yeni bir örneğini oluşturur ve nesneler arasındaki dönüşüm haritalarını yapılandırır.
        /// </summary>
        public MappingProfile()
        {
            // SignUp => User
            CreateMap<SignUp, User>()
                .ForMember(dest => dest.Biography, opt => opt.MapFrom(src => "Merhaba, ben ChatNest kullanıyorum."))
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
                .ForMember(dest => dest.Biography, opt => opt.MapFrom(src => "Merhaba, ben ChatNest kullanıyorum."))
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
