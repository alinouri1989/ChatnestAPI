using ChatNest.Services.Exceptions;
using System.Text.RegularExpressions;

namespace ChatNest.Services.Utilities
{
    /// <summary>
    /// کلاس کمکی برای اعتبارسنجی فیلدها.
    /// </summary>
    internal static class FieldValidationHelper
    {
        /// <summary>
        /// بررسی می‌کند فیلدهای داده‌شده خالی نباشند و برای موارد خالی BadRequestException پرتاب می‌کند.
        /// </summary>
        /// <param name="fields">فیلدهایی که باید بررسی شوند به‌همراه نام آن‌ها</param>
        /// <exception cref="BadRequestException">اگر هر فیلدی خالی باشد، استثنا پرتاب می‌شود.</exception>
        public static void ValidateRequiredFields(params (string Value, string FieldName)[] fields)
        {
            foreach (var (value, fieldName) in fields)
            {
                if (String.IsNullOrEmpty(value))
                {
                    throw new BadRequestException($"{fieldName} الزامی است.");
                }
            }
        }



        /// <summary>
        /// قالب آدرس ایمیل را اعتبارسنجی می‌کند. اگر معتبر نباشد، BadRequestException پرتاب می‌کند.
        /// </summary>
        /// <param name="email">آدرس ایمیلی که باید اعتبارسنجی شود</param>
        /// <param name="fieldName">نام فیلد آدرس ایمیل (به‌صورت پیش‌فرض "Email")</param>
        /// <exception cref="BadRequestException">اگر قالب ایمیل معتبر نباشد، استثنا پرتاب می‌شود.</exception>
        public static void ValidateEmailFormat(string email, string fieldName = "Email")
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new BadRequestException($"{fieldName} الزامی است.");
            }

            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, emailPattern))
            {
                throw new BadRequestException($"{fieldName} یک آدرس ایمیل معتبر نیست.");
            }
        }
    }
}
