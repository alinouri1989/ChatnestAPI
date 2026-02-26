using AutoMapper;
using ChatNest.Core.Abstract;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace ChatNest.Services.Concrete
{
    public sealed class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthRepository _authRepository;
        private readonly UserManager<User> _userManager;
        private readonly IJwtManager _jwtManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly IMapper _mapper;

        public AuthService(
            IAuthRepository authRepository,
            IUserRepository userRepository,
            UserManager<User> userManager,
            IJwtManager jwtManager,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<AuthService> logger,
            IMapper mapper)
        {
            _authRepository = authRepository;
            _userRepository = userRepository;
            _userManager = userManager;
            _jwtManager = jwtManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<IdentityResult> SignUpAsync(SignUp dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                throw new BadRequestException("Email is required");
            }

            var identityUser = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                DisplayName = dto.DisplayName.Trim(),
                BirthDate = dto.BirthDate.ToShortDateString(),
                CreatedDate = DateTime.UtcNow,
            };

            identityUser.UserIdentifier = await UserIdentifierHelper.GenerateUniqueAsync(
                _userManager,
                identityUser.DisplayName);

            var result = await _userManager.CreateAsync(identityUser, dto.Password);

            if (result.Succeeded)
            {
                return result;
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException(errors);
            }
        }

        public async Task<string> SignInEmailAsync(SignInEmail dto)
        {
            var result = await _authRepository.SignInWithEmailAsync(dto.Email, dto.Password);

            if (result.Succeeded)
            {
                var user = await _authRepository.FindByEmailAsync(dto.Email);
                return await Task.Run(() => _jwtManager.GenerateToken(user!.Id));
            }

            throw new BadRequestException("نام کاربری یا کلمه عبور صحیح نمی باشد");
        }

        public async Task<string> SignInGoogleAsync(SignInProvider dto)
        {
            var user = await _userRepository.GetUserByProviderIdAsync(dto.Uid);
            var providerData = dto.ProviderData?.FirstOrDefault();

            if (user == null && !string.IsNullOrWhiteSpace(providerData?.Email))
            {
                user = await _userRepository.GetUserByEmailAsync(providerData.Email);
            }

            if (user == null && providerData != null)
            {
                user = _mapper.Map<User>(providerData);
                user.ProviderId = dto.Uid;
                user.UserIdentifier = await UserIdentifierHelper.GenerateUniqueAsync(
                    _userManager,
                    user.DisplayName);
                await _userRepository.CreateUserAsync(user);
            }
            else if (user != null && user.ProviderId != dto.Uid)
            {
                user.ProviderId = dto.Uid;
                await _userRepository.UpdateUserAsync(user);
            }

            if (user == null)
            {
                throw new BadRequestException("Invalid provider payload");
            }

            return await Task.Run(() => _jwtManager.GenerateToken(user.Id));
        }

        public async Task<string> SignInFacebookAsync(SignInProvider dto)
        {
            var user = await _userRepository.GetUserByProviderIdAsync(dto.Uid);
            var providerData = dto.ProviderData?.FirstOrDefault();

            if (user == null && !string.IsNullOrWhiteSpace(providerData?.Email))
            {
                user = await _userRepository.GetUserByEmailAsync(providerData.Email);
            }

            if (user == null && providerData != null)
            {
                user = _mapper.Map<User>(providerData);
                user.ProviderId = dto.Uid;
                user.UserIdentifier = await UserIdentifierHelper.GenerateUniqueAsync(
                    _userManager,
                    user.DisplayName);
                await _userRepository.CreateUserAsync(user);
            }
            else if (user != null && user.ProviderId != dto.Uid)
            {
                user.ProviderId = dto.Uid;
                await _userRepository.UpdateUserAsync(user);
            }

            if (user == null)
            {
                throw new BadRequestException("Invalid provider payload");
            }

            return await Task.Run(() => _jwtManager.GenerateToken(user.Id));
        }

        public async Task ResetPasswordAsync(string email)
        {
            _logger.LogInformation("ResetPassword requested for {Email}", email);
            FieldValidationHelper.ValidateEmailFormat(email);

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user != null)
            {
                _logger.LogInformation("ResetPassword user found for {Email}. UserId={UserId}", email, user.Id);
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
                _logger.LogInformation(
                    "Password reset token generated for {Email}. RawTokenLength={RawTokenLength}, EncodedTokenLength={EncodedTokenLength}",
                    email,
                    token.Length,
                    encodedToken.Length);

                var clientBaseUrl = (_configuration["PasswordReset:ClientBaseUrl"] ?? string.Empty).TrimEnd('/');
                var resetPath = _configuration["PasswordReset:Path"] ?? "/reset-password/confirm";
                if (!resetPath.StartsWith('/'))
                {
                    resetPath = "/" + resetPath;
                }

                if (string.IsNullOrWhiteSpace(clientBaseUrl))
                {
                    _logger.LogError("Password reset client URL missing. ConfigKey=PasswordReset:ClientBaseUrl");
                    throw new InvalidOperationException("Password reset client URL is not configured.");
                }

                var resetUrl = $"{clientBaseUrl}{resetPath}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(encodedToken)}";
                _logger.LogInformation(
                    "Password reset link generated for {Email}. ClientBaseUrl={ClientBaseUrl}, ResetPath={ResetPath}, UrlLength={UrlLength}",
                    email,
                    clientBaseUrl,
                    resetPath,
                    resetUrl.Length);

                var htmlBody = $"""
                    <div style="font-family:Tahoma,Arial,sans-serif;direction:rtl;text-align:right;line-height:1.8">
                        <h2>بازیابی رمز عبور ChatNest</h2>
                        <p>برای تنظیم رمز عبور جدید، روی دکمه زیر کلیک کنید:</p>
                        <p>
                            <a href="{resetUrl}" style="background:#0f6fff;color:#fff;padding:10px 16px;border-radius:8px;text-decoration:none;display:inline-block">
                                بازیابی رمز عبور
                            </a>
                        </p>
                        <p>اگر شما این درخواست را ثبت نکرده‌اید، این ایمیل را نادیده بگیرید.</p>
                        <p style="font-size:12px;color:#666">لینک مستقیم: {resetUrl}</p>
                    </div>
                    """;

                _logger.LogInformation("Calling email service for password reset. To={Email}", email);
                try
                {
                    await _emailService.SendEmailAsync(email, "بازیابی رمز عبور ChatNest", htmlBody);
                    _logger.LogInformation("Password reset email flow completed for {Email}", email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Password reset email flow failed for {Email}", email);
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("ResetPassword requested for unknown email {Email}", email);
                throw new NotFoundException("کاربر یافت نشد.");
            }
        }

        public async Task ConfirmResetPasswordAsync(ResetPasswordConfirm dto)
        {
            FieldValidationHelper.ValidateEmailFormat(dto.Email);

            var user = await _userRepository.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(dto.Token));
            }
            catch (FormatException)
            {
                throw new BadRequestException("توکن بازیابی نامعتبر است.");
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new BadRequestException(string.IsNullOrWhiteSpace(errors) ? "بازنشانی رمز عبور انجام نشد." : errors);
            }
        }

        public async Task<PasswordSecurityQuestionPrompt> GetPasswordFallbackQuestionAsync(string email)
        {
            FieldValidationHelper.ValidateEmailFormat(email);

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
            {
                throw new NotFoundException("کاربر یافت نشد.");
            }

            await EnsureSecurityQuestionDefaultsAsync(user);

            var settings = user.UserSettings;
            return new PasswordSecurityQuestionPrompt
            {
                QuestionKey = settings.SecurityQuestionKey,
                QuestionText = settings.SecurityQuestionText,
                HasAnswerConfigured = settings.SecurityQuestionAnswerConfigured
            };
        }

        public async Task ResetPasswordByIdentityAsync(ResetPasswordFallback dto)
        {
            _logger.LogWarning("Temporary fallback password reset with security question requested for {Email}", dto.Email);
            FieldValidationHelper.ValidateEmailFormat(dto.Email);

            var user = await _userRepository.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                _logger.LogWarning("Fallback reset requested for unknown email {Email}", dto.Email);
                throw new NotFoundException("کاربر یافت نشد.");
            }

            await EnsureSecurityQuestionDefaultsAsync(user);

            var settings = user.UserSettings;
            if (!settings.SecurityQuestionAnswerConfigured)
            {
                _logger.LogWarning(
                    "Fallback reset denied because security answer is not configured for {Email}. QuestionKey={QuestionKey}",
                    dto.Email,
                    settings.SecurityQuestionKey);
                throw new BadRequestException("برای این حساب هنوز پاسخ پرسش امنیتی ثبت نشده است. لطفاً پس از ورود به حساب، آن را تنظیم کنید.");
            }

            var requestedQuestionKey = SecurityQuestionHelper.NormalizeKey(dto.QuestionKey);
            var storedQuestionKey = SecurityQuestionHelper.NormalizeKey(settings.SecurityQuestionKey);
            if (!string.Equals(requestedQuestionKey, storedQuestionKey, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Fallback reset question key mismatch for {Email}. Requested={RequestedKey}, Stored={StoredKey}",
                    dto.Email,
                    requestedQuestionKey,
                    storedQuestionKey);
                throw new BadRequestException("پرسش امنیتی انتخاب‌شده با حساب کاربری مطابقت ندارد.");
            }

            if (!SecurityQuestionHelper.VerifyAnswer(dto.Answer, settings.SecurityQuestionAnswerHash))
            {
                _logger.LogWarning("Fallback reset security answer mismatch for {Email}", dto.Email);
                throw new BadRequestException("پاسخ پرسش امنیتی صحیح نیست.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Fallback reset failed for {Email}: {Errors}", dto.Email, errors);
                throw new BadRequestException(string.IsNullOrWhiteSpace(errors) ? "بازنشانی رمز عبور انجام نشد." : errors);
            }

            _logger.LogWarning("Temporary fallback password reset succeeded for {Email}", dto.Email);
        }

        private async Task EnsureSecurityQuestionDefaultsAsync(User user)
        {
            var settings = user.UserSettings;
            var changed = false;

            if (string.IsNullOrWhiteSpace(settings.SecurityQuestionKey) || !SecurityQuestionHelper.IsKnownKey(settings.SecurityQuestionKey))
            {
                var defaultKey = SecurityQuestionHelper.GetDefaultQuestionKey(user.Id);
                settings.SecurityQuestionKey = defaultKey;
                settings.SecurityQuestionText = SecurityQuestionHelper.ResolveQuestionText(defaultKey, null);
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(settings.SecurityQuestionText))
            {
                settings.SecurityQuestionText = SecurityQuestionHelper.ResolveQuestionText(settings.SecurityQuestionKey, null);
                changed = true;
            }

            var computedConfigured = !string.IsNullOrWhiteSpace(settings.SecurityQuestionAnswerHash);
            if (settings.SecurityQuestionAnswerConfigured != computedConfigured)
            {
                settings.SecurityQuestionAnswerConfigured = computedConfigured;
                changed = true;
            }

            if (changed)
            {
                user.UserSettings = settings;
                await _userRepository.UpdateUserAsync(user);
            }
        }

        private static bool IsBirthDateMatch(string? storedBirthDate, DateTime providedBirthDate)
        {
            if (string.IsNullOrWhiteSpace(storedBirthDate))
            {
                return false;
            }

            if (DateTime.TryParse(storedBirthDate, out var parsedStored))
            {
                return parsedStored.Date == providedBirthDate.Date;
            }

            var candidates = new[]
            {
                providedBirthDate.ToShortDateString(),
                providedBirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                providedBirthDate.ToString("M/d/yyyy", CultureInfo.InvariantCulture),
                providedBirthDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                providedBirthDate.ToString("d/M/yyyy", CultureInfo.InvariantCulture),
                providedBirthDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            };

            return candidates.Any(candidate =>
                string.Equals(candidate, storedBirthDate.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
