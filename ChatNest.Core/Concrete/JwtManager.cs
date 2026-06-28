using ChatNest.Core.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace ChatNest.Core.Concrete
{
    /// <summary>
    /// کلاسی که فرایند تولید JSON Web Token (JWT) را مدیریت می‌کند.
    /// </summary>
    /// <remarks>
    /// این کلاس تنظیمات لازم برای تولید JWT را دریافت می‌کند
    /// و با متد <see cref="GenerateToken"/> توکن ایجاد می‌کند.
    /// </remarks>
    public sealed class JwtManager : IJwtManager
    {
        private readonly string? _secret;
        private readonly string? _issuer;
        private readonly string? _audience;
        private readonly byte _expiryInDays;



        /// <summary>
        /// سازنده کلاس <see cref="JwtManager"/> است.
        /// برای دریافت تنظیمات JWT از شیء <see cref="IConfiguration"/> استفاده می‌کند.
        /// </summary>
        /// <param name="configuration">شیء <see cref="IConfiguration"/> شامل تنظیمات JWT.</param>
        public JwtManager(IConfiguration configuration)
        {
            _secret = configuration["JWT:SecretKey"];
            _issuer = configuration["JWT:Issuer"];
            _audience = configuration["JWT:Audience"];
            _expiryInDays = byte.Parse(configuration["JWT:ExpiryInDays"]!);
        }



        /// <summary>
        /// برای شناسه کاربر داده‌شده یک JSON Web Token (JWT) ایجاد می‌کند.
        /// </summary>
        /// <param name="userId">شناسه کاربری که برای تولید JWT استفاده می‌شود.</param>
        /// <returns>رشته‌ای که JWT تولیدشده را نمایش می‌دهد.</returns>
        /// <exception cref="ArgumentNullException">در صورت نامعتبر یا ناقص بودن داده‌های پیکربندی پرتاب می‌شود.</exception>
        public string GenerateToken(string userId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(_expiryInDays),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = credentials
            };

            return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
        }
    }
}
