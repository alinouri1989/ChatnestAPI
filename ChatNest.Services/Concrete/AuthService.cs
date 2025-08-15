using AutoMapper;
using ChatNest.Core.Abstract;
using ChatNest.DataAccess.Abstract;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Utilities;
using ChatNest.Shared.DTOs.Request;
using Microsoft.AspNetCore.Identity;

namespace ChatNest.Services.Concrete
{
    public sealed class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthRepository _authRepository;
        private readonly UserManager<User> _userManager;
        private readonly IJwtManager _jwtManager;
        private readonly IMapper _mapper;

        public AuthService(
            IAuthRepository authRepository,
            IUserRepository userRepository,
            UserManager<User> userManager,
            IJwtManager jwtManager,
            IMapper mapper)
        {
            _authRepository = authRepository;
            _userRepository = userRepository;
            _userManager = userManager;
            _jwtManager = jwtManager;
            _mapper = mapper;
        }

        public async Task SignUpAsync(SignUp dto)
        {
            var User = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                CreatedDate = DateTime.UtcNow,
                LastConnectionDate = DateTime.UtcNow,
                ProviderId = "email"
            };

            var result = await _authRepository.CreateUserAsync(User, dto.Password);

            if (result.Succeeded)
            {
                var user = _mapper.Map<User>(dto);
                user.Id = User.Id;
                await _userRepository.CreateUserAsync(user);
            }
            else
            {
                throw new BadRequestException(string.Join(", ", result.Errors.Select(e => e.Description)));
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
            // Implement Google sign-in logic
            var user = await _userRepository.GetUserByProviderIdAsync(dto.Uid);

            if (user == null)
            {
                user = _mapper.Map<User>(dto.ProviderData[0]);
                user.ProviderId = "google.com";
                await _userRepository.CreateUserAsync(user);
            }

            return await Task.Run(() => _jwtManager.GenerateToken(dto.Uid));
        }

        public async Task<string> SignInFacebookAsync(SignInProvider dto)
        {
            // Implement Facebook sign-in logic
            var user = await _userRepository.GetUserByProviderIdAsync(dto.Uid);

            if (user == null)
            {
                user = _mapper.Map<User>(dto.ProviderData[0]);
                user.ProviderId = "facebook.com";
                await _userRepository.CreateUserAsync(user);
            }

            return await Task.Run(() => _jwtManager.GenerateToken(dto.Uid));
        }

        public async Task ResetPasswordAsync(string email)
        {
            FieldValidationHelper.ValidateEmailFormat(email);

            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user != null)
            {
                await _authRepository.ResetPasswordAsync(email);
            }
            else
            {
                throw new NotFoundException("Kullanıcı bulunamadı.");
            }
        }
    }
}