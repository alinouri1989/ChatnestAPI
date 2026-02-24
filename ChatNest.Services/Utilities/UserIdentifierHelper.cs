using ChatNest.Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ChatNest.Services.Utilities
{
    internal static class UserIdentifierHelper
    {
        public static string NormalizeCandidate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(value.Trim(), @"\s+", "_");
            normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_]", string.Empty);
            normalized = normalized.Trim('_');

            if (normalized.Length > 30)
            {
                normalized = normalized[..30];
            }

            return normalized;
        }

        public static async Task<string> GenerateUniqueAsync(
            UserManager<User> userManager,
            string? preferredValue,
            string? excludeUserId = null)
        {
            var baseValue = NormalizeCandidate(preferredValue);
            if (string.IsNullOrWhiteSpace(baseValue))
            {
                baseValue = $"user{Random.Shared.Next(1000, 9999)}";
            }

            if (baseValue.Length < 4)
            {
                baseValue = $"{baseValue}{new string('0', 4 - baseValue.Length)}";
            }

            baseValue = baseValue[..Math.Min(baseValue.Length, 24)];

            var candidate = baseValue;
            var attempt = 0;

            while (await userManager.Users.AnyAsync(u =>
                       u.UserIdentifier == candidate &&
                       (excludeUserId == null || u.Id != excludeUserId)))
            {
                attempt++;
                var suffix = attempt <= 9999
                    ? attempt.ToString("D4")
                    : Guid.NewGuid().ToString("N")[..6];

                var prefixMax = Math.Max(4, 30 - suffix.Length - 1);
                var prefix = baseValue[..Math.Min(baseValue.Length, prefixMax)];
                candidate = $"{prefix}_{suffix}";
            }

            return candidate;
        }
    }
}
