using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Microsoft.Extensions.Configuration;

namespace ChatNest.DataAccess.Configurations
{
    /// <summary>
    /// پیکربندی احراز هویت فایربیس و دسترسی به پایگاه داده را مدیریت می‌کند.
    /// </summary>
    public class FirebaseConfig
    {
        /// <summary>شیء کلاینت مورد استفاده برای عملیات احراز هویت فایربیس.</summary>
        public FirebaseAuthClient AuthClient { get; }



        /// <summary>شیء کلاینت مورد استفاده برای برقراری ارتباط با Firebase Realtime Database.</summary>
        public FirebaseClient DatabaseClient { get; }



        // <summary>
        /// کلاینت‌های احراز هویت و پایگاه داده فایربیس را بر اساس شیء <see cref="IConfiguration"/> پیکربندی می‌کند.
        /// </summary>
        /// <param name="configuration">شیء <see cref="IConfiguration"/> شامل تنظیمات پیکربندی برنامه.</param>
        public FirebaseConfig(IConfiguration configuration)
        {
            var apiKey = configuration["Firebase:apiKey"];
            var authDomain = configuration["Firebase:authDomain"];
            var databaseUrl = configuration["Firebase:databaseUrl"];

            var config = new FirebaseAuthConfig
            {
                ApiKey = apiKey,
                AuthDomain = authDomain,
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider(),
                    new GoogleProvider().AddScopes("email", "profile", "openid"),
                    new FacebookProvider(),
                }
            };

            AuthClient = new FirebaseAuthClient(config);

            DatabaseClient = new FirebaseClient(databaseUrl);
        }
    }
}
