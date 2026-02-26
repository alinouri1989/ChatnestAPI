using System.Security.Cryptography;
using System.Text;

namespace ChatNest.Services.Utilities
{
    public sealed record SecurityQuestionDefinition(string Key, string Text);

    public static class SecurityQuestionHelper
    {
        public const string CustomQuestionKey = "custom";

        private static readonly IReadOnlyList<SecurityQuestionDefinition> _definitions = new List<SecurityQuestionDefinition>
        {
            new("private_phrase", "عبارت محرمانه‌ای که فقط خودتان برای بازیابی حساب تعیین می‌کنید چیست؟"),
            new("home_nickname", "نام یا لقبی که فقط اعضای نزدیک خانواده شما استفاده می‌کنند چیست؟"),
            new("personal_hint", "کلمه یادآور شخصی شما برای مواقع اضطراری چیست؟"),
            new("first_goal", "کلمه کلیدی یکی از هدف‌های شخصی قدیمی شما که فقط خودتان می‌دانید چیست؟"),
            new(CustomQuestionKey, "سؤال اختصاصی (پیشنهاد می‌شود سؤال شخصی و غیرقابل‌حدس انتخاب کنید)")
        };

        private static readonly string[] _publicQuestionKeywords =
        {
            "نام مادر",
            "مادر",
            "نام پدر",
            "پدر",
            "محل تولد",
            "تاریخ تولد",
            "شماره ملی",
            "کد ملی",
            "شماره شناسنامه",
            "مدرسه",
            "شهر"
        };

        public static IReadOnlyList<SecurityQuestionDefinition> GetDefinitions() => _definitions;

        public static string GetDefaultQuestionKey(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return _definitions[0].Key;
            }

            var sum = 0;
            foreach (var ch in userId)
            {
                sum += ch;
            }

            var candidateKeys = _definitions.Where(d => d.Key != CustomQuestionKey).Select(d => d.Key).ToArray();
            return candidateKeys[sum % candidateKeys.Length];
        }

        public static bool IsKnownKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _definitions.Any(d => string.Equals(d.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeKey(string? key)
        {
            return (key ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static string ResolveQuestionText(string? key, string? customQuestionText)
        {
            var normalizedKey = NormalizeKey(key);
            if (normalizedKey == CustomQuestionKey)
            {
                var normalizedCustom = NormalizeCustomQuestion(customQuestionText);
                if (string.IsNullOrWhiteSpace(normalizedCustom))
                {
                    throw new ArgumentException("متن سؤال اختصاصی الزامی است.");
                }

                ValidateCustomQuestion(normalizedCustom);
                return normalizedCustom;
            }

            var definition = _definitions.FirstOrDefault(d => d.Key == normalizedKey);
            if (definition == null)
            {
                throw new ArgumentException("سؤال امنیتی انتخاب‌شده معتبر نیست.");
            }

            return definition.Text;
        }

        public static string NormalizeCustomQuestion(string? customQuestionText)
        {
            return (customQuestionText ?? string.Empty).Trim();
        }

        public static void ValidateCustomQuestion(string customQuestionText)
        {
            if (customQuestionText.Length < 10 || customQuestionText.Length > 200)
            {
                throw new ArgumentException("سؤال اختصاصی باید بین 10 تا 200 کاراکتر باشد.");
            }

            if (_publicQuestionKeywords.Any(keyword => customQuestionText.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("لطفاً از سؤال‌های عمومی یا قابل‌حدس استفاده نکنید.");
            }
        }

        public static string NormalizeAnswer(string? answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                return string.Empty;
            }

            var trimmed = answer.Trim();
            var compact = string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return compact.ToUpperInvariant();
        }

        public static string HashAnswer(string? answer)
        {
            var normalized = NormalizeAnswer(answer);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(bytes);
        }

        public static bool VerifyAnswer(string? answer, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            var computedHash = HashAnswer(answer);
            if (string.IsNullOrWhiteSpace(computedHash) || computedHash.Length != storedHash.Trim().Length)
            {
                return false;
            }

            var left = Encoding.UTF8.GetBytes(computedHash);
            var right = Encoding.UTF8.GetBytes(storedHash.Trim());
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
