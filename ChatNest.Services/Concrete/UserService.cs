using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Identity;

namespace ChatNest.Services.Concrete
{
    public sealed class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ICloudRepository _cloudRepository;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;

        public UserService(
            IUserRepository userRepository,
            ICloudRepository cloudRepository,
            UserManager<User> userManager,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _cloudRepository = cloudRepository;
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<Dictionary<string, FoundUsers>> SearchUsersAsync(string userId, string query)
        {
            var users = await _userRepository.SearchUsersAsync(query);
            var filteredUsers = users.Where(u => u.Id != userId);

            var result = new Dictionary<string, FoundUsers>();
            foreach (var user in filteredUsers)
            {
                result.Add(user.Id, _mapper.Map<FoundUsers>(user));
            }

            return result;
        }

        public async Task<UserInfo> GetUserInfoAsync(string userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            return _mapper.Map<UserInfo>(user);
        }

        public async Task<Dictionary<string, CallerUser>> GetUserProfilesAsync(List<string> recipientIds)
        {
            var result = new Dictionary<string, CallerUser>();

            foreach (var id in recipientIds)
            {
                var user = await _userRepository.GetUserByIdAsync(id);
                if (user != null)
                {
                    result.Add(id, _mapper.Map<CallerUser>(user));
                }
            }

            return result;
        }

        public async Task<Uri> RemoveProfilePhotoAsync(string userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.ProfilePhoto = null;
            await _userRepository.UpdateUserAsync(user);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.ProfilePhoto = null;
                await _userManager.UpdateAsync(appUser);
            }

            return new Uri("https://example.com/default-avatar.png");
        }

        public async Task<Uri> UpdateProfilePhotoAsync(string userId, UpdateProfilePhoto dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");
            byte[] photoBytes;

            var base64Data = dto.ProfilePhoto.Contains(',') ? dto.ProfilePhoto.Split(',')[1] : dto.ProfilePhoto;
            photoBytes = Convert.FromBase64String(base64Data);

            using var photoStream = new MemoryStream(photoBytes);
            var photoUrl = await _cloudRepository.UploadPhotoAsync(
                $"profile_{userId}",
                "profiles",
                "profile,user",
                photoStream);

            user.ProfilePhoto = photoUrl;
            await _userRepository.UpdateUserAsync(user);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.ProfilePhoto = photoUrl;
                await _userManager.UpdateAsync(appUser);
            }

            return photoUrl;
        }

        public async Task UpdateDisplayNameAsync(string userId, UpdateDisplayName dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.DisplayName = dto.DisplayName;
            await _userRepository.UpdateUserAsync(user);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.DisplayName = dto.DisplayName;
                await _userManager.UpdateAsync(appUser);
            }
        }

        public async Task UpdatePhoneNumberAsync(string userId, UpdatePhoneNumber dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.PhoneNumber = dto.PhoneNumber;
            await _userRepository.UpdateUserAsync(user);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.PhoneNumber = dto.PhoneNumber;
                await _userManager.UpdateAsync(appUser);
            }
        }

        public async Task UpdateBiographyAsync(string userId, UpdateBiography dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.Biography = dto.Biography;
            await _userRepository.UpdateUserAsync(user);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.Biography = dto.Biography;
                await _userManager.UpdateAsync(appUser);
            }
        }

        public async Task ChangePasswordAsync(string userId, ChangePassword dto)
        {
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser == null)
                throw new NotFoundException("User not found");

            var result = await _userManager.ChangePasswordAsync(appUser, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        public async Task ChangeThemeAsync(string userId, ChangeTheme dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.UserSettings.Theme = dto.Theme;
            await _userRepository.UpdateUserAsync(user);
        }

        public async Task ChangeChatBackgroundAsync(string userId, ChangeChatBackground dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            // Assuming you have a ChatBackground property in UserSettings
            // user.UserSettings.ChatBackground = dto.ChatBackground;
            await _userRepository.UpdateUserAsync(user);
        }

        public async Task<Dictionary<string, RecipientProfile>> GetRecipientProfilesAsync(List<string> recipientIds)
        {
            var result = new Dictionary<string, RecipientProfile>();

            foreach (var id in recipientIds)
            {
                var user = await _userRepository.GetUserByIdAsync(id);
                if (user != null)
                {
                    result.Add(id, _mapper.Map<RecipientProfile>(user));
                }
            }

            return result;
        }

        public async Task UpdateLastConnectionDateAsync(string userId, DateTime lastConnectionDate)
        {
            await _userRepository.UpdateLastConnectionDateAsync(userId, lastConnectionDate);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.LastConnectionDate = lastConnectionDate;
                await _userManager.UpdateAsync(appUser);
            }
        }
    }
}