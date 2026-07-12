using AutoMapper;
using ChatNest.Core.Abstract;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Identity;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs.Request;
using ChatNest.Shared.DTOs.Response;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
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
        private readonly ISmsOtpSender _smsOtpSender;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<AuthService> _logger;
        private readonly IMapper _mapper;

        public AuthService(
            IAuthRepository authRepository,
            IUserRepository userRepository,
            UserManager<User> userManager,
            IJwtManager jwtManager,
            IEmailService emailService,
            ISmsOtpSender smsOtpSender,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            ILogger<AuthService> logger,
            IMapper mapper)
        {
            _authRepository = authRepository;
            _userRepository = userRepository;
            _userManager = userManager;
            _jwtManager = jwtManager;
            _emailService = emailService;
            _smsOtpSender = smsOtpSender;
            _configuration = configuration;
            _memoryCache = memoryCache;
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
                PhoneNumber = NormalizeMobile(dto.PhoneNumber),
                MobileNo = NormalizeMobile(dto.PhoneNumber),
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

        public async Task<AuthTokenResponse> SignInEmailAsync(SignInEmail dto)
        {
            var result = await _authRepository.SignInWithEmailAsync(dto.Email, dto.Password);

            if (result.Succeeded)
            {
                var user = await _authRepository.FindByEmailAsync(dto.Email);
                return await IssueTokensAsync(user!.Id);
            }

            throw new BadRequestException("نام کاربری یا کلمه عبور صحیح نمی باشد");
        }

        public async Task RequestLoginOtpAsync(RequestLoginOtp dto)
        {
            var email = NormalizeEmail(dto.Email);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var userByEmail = await _userManager.FindByEmailAsync(email);
                if (userByEmail == null)
                {
                    throw new NotFoundException("کاربری با این ایمیل یافت نشد.");
                }

                var emailCode = GenerateOtpCode();
                _memoryCache.Set(
                    BuildEmailOtpCacheKey(email),
                    HashOtp(email, emailCode),
                    TimeSpan.FromMinutes(GetOtpExpiryMinutes()));

                await SendEmailOtpAsync(email, emailCode);
                return;
            }

            var mobile = NormalizeMobile(dto.Mobile);
            if (string.IsNullOrWhiteSpace(mobile))
            {
                throw new BadRequestException("ایمیل یا شماره موبایل معتبر وارد کنید.");
            }

            var user = await FindUserByMobileAsync(mobile);
            if (user == null)
            {
                throw new NotFoundException("کاربری با این شماره موبایل یافت نشد.");
            }

            var code = GenerateOtpCode();
            _memoryCache.Set(
                BuildOtpCacheKey(mobile),
                HashOtp(mobile, code),
                TimeSpan.FromMinutes(GetOtpExpiryMinutes()));

            await _smsOtpSender.SendOtpAsync(mobile, code);
        }

        public async Task<AuthTokenResponse> VerifyLoginOtpAsync(VerifyLoginOtp dto)
        {
            var email = NormalizeEmail(dto.Email);
            if (!string.IsNullOrWhiteSpace(email))
            {
                if (string.IsNullOrWhiteSpace(dto.Code))
                {
                    throw new BadRequestException("کد تأیید معتبر نیست.");
                }

                if (!_memoryCache.TryGetValue<string>(BuildEmailOtpCacheKey(email), out var expectedEmailHash) ||
                    !string.Equals(expectedEmailHash, HashOtp(email, dto.Code), StringComparison.Ordinal))
                {
                    throw new BadRequestException("کد تأیید صحیح نیست یا منقضی شده است.");
                }

                var userByEmail = await _userManager.FindByEmailAsync(email);
                if (userByEmail == null)
                {
                    throw new NotFoundException("کاربری با این ایمیل یافت نشد.");
                }

                _memoryCache.Remove(BuildEmailOtpCacheKey(email));
                return await IssueTokensAsync(userByEmail.Id);
            }

            var mobile = NormalizeMobile(dto.Mobile);
            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new BadRequestException("شماره موبایل یا کد تأیید معتبر نیست.");
            }

            if (!_memoryCache.TryGetValue<string>(BuildOtpCacheKey(mobile), out var expectedHash) ||
                !string.Equals(expectedHash, HashOtp(mobile, dto.Code), StringComparison.Ordinal))
            {
                throw new BadRequestException("کد تأیید صحیح نیست یا منقضی شده است.");
            }

            var user = await FindUserByMobileAsync(mobile);
            if (user == null)
            {
                throw new NotFoundException("کاربری با این شماره موبایل یافت نشد.");
            }

            _memoryCache.Remove(BuildOtpCacheKey(mobile));
            if (!user.MobileConfirmed || !user.PhoneNumberConfirmed)
            {
                user.MobileConfirmed = true;
                user.PhoneNumberConfirmed = true;
                await _userRepository.UpdateUserAsync(user);
            }

            return await IssueTokensAsync(user.Id);
        }

        public async Task<AuthTokenResponse> SignInGoogleAsync(SignInProvider dto)
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

            return await IssueTokensAsync(user.Id);
        }

        public async Task<AuthTokenResponse> SignInFacebookAsync(SignInProvider dto)
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

            return await IssueTokensAsync(user.Id);
        }

        public async Task<AuthTokenResponse> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new UnauthorizedAccessException("Refresh token is required.");
            }

            var replacement = CreateRefreshToken();
            var userId = await _authRepository.RotateRefreshTokenAsync(
                HashRefreshToken(refreshToken),
                replacement.Entity);

            if (userId == null)
            {
                throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
            }

            return new AuthTokenResponse
            {
                Token = _jwtManager.GenerateToken(userId),
                RefreshToken = replacement.RawToken,
                RefreshTokenExpiration = replacement.Entity.Expiration
            };
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _authRepository.RevokeRefreshTokenAsync(HashRefreshToken(refreshToken));
            }
        }

        private async Task<AuthTokenResponse> IssueTokensAsync(string userId)
        {
            var refreshToken = CreateRefreshToken();
            refreshToken.Entity.UserId = userId;
            await _authRepository.AddRefreshTokenAsync(refreshToken.Entity);

            return new AuthTokenResponse
            {
                Token = _jwtManager.GenerateToken(userId),
                RefreshToken = refreshToken.RawToken,
                RefreshTokenExpiration = refreshToken.Entity.Expiration
            };
        }

        private (string RawToken, RefreshToken Entity) CreateRefreshToken()
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            var expiryInDays = _configuration.GetValue<int?>("JWT:RefreshTokenExpiryInDays") ?? 30;

            return (rawToken, new RefreshToken
            {
                Token = HashRefreshToken(rawToken),
                Created = DateTime.UtcNow,
                Expiration = DateTime.UtcNow.AddDays(expiryInDays),
                IsActive = true
            });
        }

        private static string HashRefreshToken(string refreshToken)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }

        private static string NormalizeMobile(string? mobile)
        {
            var value = new string((mobile ?? string.Empty).Where(ch => char.IsDigit(ch) || ch == '+').ToArray()).Trim();
            if (value.StartsWith("0098", StringComparison.Ordinal))
            {
                return "+98" + value[4..];
            }

            if (value.StartsWith("98", StringComparison.Ordinal) && value.Length == 12)
            {
                return "+98" + value[2..];
            }

            if (value.StartsWith("09", StringComparison.Ordinal) && value.Length == 11)
            {
                return "+98" + value[1..];
            }

            return value;
        }

        private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim();

        private int GetOtpExpiryMinutes() => _configuration.GetValue<int?>("Kavenegar:OtpExpiryMinutes") ?? 2;

        private static string GenerateOtpCode() =>
            RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);

        private Task SendEmailOtpAsync(string email, string code)
        {
            return _emailService.SendEmailAsync(email, new Dictionary<string, string>
            {
                ["otp"] = code,
                ["app_name"] = "ChatNest",
                ["expires_minutes"] = GetOtpExpiryMinutes().ToString(CultureInfo.InvariantCulture)
            });
        }

        private static string BuildOtpCacheKey(string mobile) => $"login-otp:{mobile}";

        private static string BuildEmailOtpCacheKey(string email) => $"login-otp-email:{email}";

        private static string HashOtp(string identifier, string code)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{identifier}:{code.Trim()}")));
        }

        private async Task<User?> FindUserByMobileAsync(string mobile)
        {
            var localMobile = mobile.Replace("+98", "0", StringComparison.Ordinal);

            // PhoneNumber is the canonical value exposed and edited by the user profile.
            // Check it first so a stale legacy MobileNo on another account cannot win an
            // unordered FirstOrDefault query and issue a token for the wrong user.
            var user = await _userManager.Users.FirstOrDefaultAsync(candidate =>
                candidate.PhoneNumber == mobile || candidate.PhoneNumber == localMobile);

            if (user != null)
            {
                return user;
            }

            // MobileNo is retained only as a fallback for legacy accounts which have not
            // yet populated Identity's PhoneNumber field.
            return await _userManager.Users.FirstOrDefaultAsync(candidate =>
                string.IsNullOrEmpty(candidate.PhoneNumber) &&
                (candidate.MobileNo == mobile || candidate.MobileNo == localMobile));
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

                _logger.LogInformation("Calling email service for password reset. To={Email}", email);
                try
                {
                    await _emailService.SendEmailAsync(email, new Dictionary<string, string>
                    {
                        ["reset_url"] = resetUrl,
                        ["app_name"] = "ChatNest"
                    });
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
