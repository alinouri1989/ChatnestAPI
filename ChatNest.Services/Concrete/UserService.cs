using AutoMapper;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace ChatNest.Services.Concrete
{
    public sealed class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IMediaStorageRepository _mediaStorageRepository;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;

        public UserService(
            IUserRepository userRepository,
            IMediaStorageRepository mediaStorageRepository,
            UserManager<User> userManager,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _mediaStorageRepository = mediaStorageRepository;
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

            if (string.IsNullOrWhiteSpace(user.UserIdentifier))
            {
                user.UserIdentifier = await UserIdentifierHelper.GenerateUniqueAsync(
                    _userManager,
                    user.DisplayName,
                    user.Id);
                await _userRepository.UpdateUserAsync(user);

                var appUser = await _userManager.FindByIdAsync(userId);
                if (appUser != null)
                {
                    appUser.UserIdentifier = user.UserIdentifier;
                    await _userManager.UpdateAsync(appUser);
                }
            }

            await EnsureSecurityQuestionDefaultsAsync(user);

            var userInfo = _mapper.Map<UserInfo>(user);
            userInfo.UserSettings = CreatePublicUserSettings(user.UserSettings);
            return userInfo;
        }

        public async Task<Dictionary<string, CallerUser>> GetUserProfilesAsync(List<string> recipientIds)
        {
            var result = new Dictionary<string, CallerUser>();
            var usersById = await _userRepository.GetUsersByIdsAsync(recipientIds);
            foreach (var id in recipientIds.Distinct())
            {
                if (usersById.TryGetValue(id, out var user))
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

            return new Uri("", UriKind.Relative);
        }

        public async Task<Uri> UpdateProfilePhotoAsync(string userId, UpdateProfilePhoto dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var base64Data = dto.ProfilePhoto.Contains(',') ? dto.ProfilePhoto.Split(',')[1] : dto.ProfilePhoto;
            MemoryStream photoStream;
            try
            {
                photoStream = FileValidationHelper.ValidatePhoto(base64Data);
            }
            catch (FormatException)
            {
                throw new BadRequestException("فرمت عکس پروفایل نامعتبر است.");
            }

            using (photoStream)
            {
                var photoUrl = await _mediaStorageRepository.UploadPhotoAsync(
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
        }

        public async Task UpdateDisplayNameAsync(string userId, UpdateDisplayName dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            user.DisplayName = dto.DisplayName.Trim();
            await _userRepository.UpdateUserAsync(user);

            // Also update Identity user
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.DisplayName = dto.DisplayName.Trim();
                await _userManager.UpdateAsync(appUser);
            }
        }

        public async Task UpdateUserIdentifierAsync(string userId, UpdateUserIdentifier dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var normalized = UserIdentifierHelper.NormalizeCandidate(dto.UserIdentifier);
            if (normalized.Length < 4)
                throw new BadRequestException("شناسه کاربر معتبر نیست.");

            var uniqueIdentifier = await UserIdentifierHelper.GenerateUniqueAsync(
                _userManager,
                normalized,
                userId);

            if (!string.Equals(uniqueIdentifier, normalized, StringComparison.Ordinal))
                throw new BadRequestException("این شناسه قبلاً استفاده شده است.");

            user.UserIdentifier = uniqueIdentifier;
            await _userRepository.UpdateUserAsync(user);

            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser != null)
            {
                appUser.UserIdentifier = uniqueIdentifier;
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

            var userSettings = user.UserSettings;
            userSettings.Theme = dto.Theme;
            user.UserSettings = userSettings;
            await _userRepository.UpdateUserAsync(user);
        }

        public async Task ChangeChatBackgroundAsync(string userId, ChangeChatBackground dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var userSettings = user.UserSettings;
            userSettings.ChatBackground = dto.ChatBackground;
            user.UserSettings = userSettings;
            await _userRepository.UpdateUserAsync(user);
        }

        public async Task UpdateSecurityQuestionAsync(string userId, UpdateSecurityQuestion dto)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User not found");

            var settings = user.UserSettings;
            var normalizedKey = SecurityQuestionHelper.NormalizeKey(dto.QuestionKey);

            if (!SecurityQuestionHelper.IsKnownKey(normalizedKey))
                throw new BadRequestException("سؤال امنیتی انتخاب‌شده معتبر نیست.");

            string questionText;
            try
            {
                questionText = SecurityQuestionHelper.ResolveQuestionText(normalizedKey, dto.CustomQuestionText);
            }
            catch (ArgumentException ex)
            {
                throw new BadRequestException(ex.Message);
            }

            settings.SecurityQuestionKey = normalizedKey;
            settings.SecurityQuestionText = questionText;
            settings.SecurityQuestionAnswerHash = SecurityQuestionHelper.HashAnswer(dto.Answer);
            settings.SecurityQuestionAnswerConfigured = !string.IsNullOrWhiteSpace(settings.SecurityQuestionAnswerHash);
            settings.SecurityQuestionUpdatedAtUtc = DateTime.UtcNow;
            user.UserSettings = settings;

            await _userRepository.UpdateUserAsync(user);
        }

        public async Task<Dictionary<string, RecipientProfile>> GetRecipientProfilesAsync(List<string> recipientIds)
        {
            var result = new Dictionary<string, RecipientProfile>();
            var usersById = await _userRepository.GetUsersByIdsAsync(recipientIds);
            foreach (var id in recipientIds.Distinct())
            {
                if (usersById.TryGetValue(id, out var user))
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

        private async Task EnsureSecurityQuestionDefaultsAsync(User user)
        {
            var userSettings = user.UserSettings;
            var changed = false;

            if (string.IsNullOrWhiteSpace(userSettings.SecurityQuestionKey) || !SecurityQuestionHelper.IsKnownKey(userSettings.SecurityQuestionKey))
            {
                var defaultKey = SecurityQuestionHelper.GetDefaultQuestionKey(user.Id);
                userSettings.SecurityQuestionKey = defaultKey;
                userSettings.SecurityQuestionText = SecurityQuestionHelper.ResolveQuestionText(defaultKey, null);
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(userSettings.SecurityQuestionText))
            {
                userSettings.SecurityQuestionText = SecurityQuestionHelper.ResolveQuestionText(userSettings.SecurityQuestionKey, null);
                changed = true;
            }

            var computedConfigured = !string.IsNullOrWhiteSpace(userSettings.SecurityQuestionAnswerHash);
            if (userSettings.SecurityQuestionAnswerConfigured != computedConfigured)
            {
                userSettings.SecurityQuestionAnswerConfigured = computedConfigured;
                changed = true;
            }

            if (changed)
            {
                user.UserSettings = userSettings;
                await _userRepository.UpdateUserAsync(user);
            }
        }

        private static UserSettings CreatePublicUserSettings(UserSettings settings)
        {
            var clone = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings)) ?? new UserSettings();
            clone.SecurityQuestionAnswerConfigured = !string.IsNullOrWhiteSpace(settings.SecurityQuestionAnswerHash) || settings.SecurityQuestionAnswerConfigured;
            clone.SecurityQuestionAnswerHash = string.Empty;
            return clone;
        }
    }
}
