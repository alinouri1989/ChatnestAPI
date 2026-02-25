using AutoMapper;
using ChatNest.Core.Abstract;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Linq;
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
        private readonly IMapper _mapper;

        public AuthService(
            IAuthRepository authRepository,
            IUserRepository userRepository,
            UserManager<User> userManager,
            IJwtManager jwtManager,
            IEmailService emailService,
            IConfiguration configuration,
            IMapper mapper)
        {
            _authRepository = authRepository;
            _userRepository = userRepository;
            _userManager = userManager;
            _jwtManager = jwtManager;
            _emailService = emailService;
            _configuration = configuration;
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

            throw new BadRequestException("Invalid email or password");
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
            FieldValidationHelper.ValidateEmailFormat(email);

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));

                var clientBaseUrl = (_configuration["PasswordReset:ClientBaseUrl"] ?? string.Empty).TrimEnd('/');
                var resetPath = _configuration["PasswordReset:Path"] ?? "/reset-password/confirm";
                if (!resetPath.StartsWith('/'))
                {
                    resetPath = "/" + resetPath;
                }

                if (string.IsNullOrWhiteSpace(clientBaseUrl))
                {
                    throw new InvalidOperationException("Password reset client URL is not configured.");
                }

                var resetUrl = $"{clientBaseUrl}{resetPath}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(encodedToken)}";

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

                await _emailService.SendEmailAsync(email, "بازیابی رمز عبور ChatNest", htmlBody);
            }
            else
            {
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
    }
}
