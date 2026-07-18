using ChatNest.API.Hubs;

using ChatNest.Core.Abstract;
using ChatNest.Core.Concrete;
using ChatNest.DataAccess.Abstract;
using ChatNest.DataAccess.Concrete;
using ChatNest.DataAccess.Configurations;
using ChatNest.DataAccess.Contexts;
using ChatNest.Entities.Identity;
using ChatNest.Entities.Models;
using ChatNest.Services.Abstract;
using ChatNest.Services.Concrete;
using ChatNest.Services.Exceptions;
using ChatNest.Services.Mapping;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Serilog;
using Serilog.Events;

using System.Net;
using System.Text;
using System.Text.Json.Serialization;

using Xabe.FFmpeg;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        "Logs/startup-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    /*
     * Serilog
     */
    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                "Logs/all-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true)
            .WriteTo.File(
                "Logs/errors-.log",
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 60,
                shared: true);
    });

    /*
     * Forwarded headers
     *
     * درخواست اصلی HTTPS است، اما Nginx آن را به HTTP روی پورت 5000
     * ارسال می‌کند. این تنظیم باعث می‌شود ASP.NET Core متوجه HTTPS بودن
     * درخواست اصلی شود.
     */
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost;

        options.ForwardLimit = 1;

        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    });

    /*
     * FFmpeg
     */
    var ffmpegPath = builder.Configuration["FileStorage:PathFFmpeg"];

    if (!string.IsNullOrWhiteSpace(ffmpegPath))
    {
        FFmpeg.SetExecutablesPath(ffmpegPath);
    }

    /*
     * Database
     */
    builder.Services.AddDbContext<ChatNestDbContext>(options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"));
    });

    /*
     * Identity
     */
    builder.Services
        .AddIdentity<User, Role>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(30);

            options.Lockout.MaxFailedAccessAttempts = 5;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddRoles<Role>()
        .AddRoleManager<RoleManager<Role>>()
        .AddRoleValidator<RoleValidator<Role>>()
        .AddEntityFrameworkStores<ChatNestDbContext>()
        .AddDefaultTokenProviders();

    /*
     * Data Protection
     */
    var dataProtectionKeysPath =
        builder.Configuration["DataProtection:KeysPath"]
        ?? "/www/wwwroot/Api/keys";

    Directory.CreateDirectory(dataProtectionKeysPath);

    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(
            new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("ChatNest");

    /*
     * Application configurations
     */
    builder.Services.AddSingleton<FileStorageConfig>(_ =>
        new FileStorageConfig(builder.Configuration));

    builder.Services.AddSingleton<GeminiConfig>(_ =>
        new GeminiConfig(builder.Configuration));

    builder.Services.AddSingleton<HuggingFaceConfig>(_ =>
        new HuggingFaceConfig(builder.Configuration));

    /*
     * JWT Authentication
     */
    var jwtSettings = builder.Configuration.GetSection("JWT");

    var secretKey = jwtSettings["SecretKey"];

    if (string.IsNullOrWhiteSpace(secretKey))
    {
        throw new InvalidOperationException(
            "JWT:SecretKey is not configured.");
    }

    var jwtIssuer =
        jwtSettings["Issuer"]
        ?? "chatnest-api";

    var jwtAudience =
        jwtSettings["Audience"]
        ?? "chatnest-clients";

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                JwtBearerDefaults.AuthenticationScheme;

            options.DefaultChallengeScheme =
                JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;

            /*
             * Authority در اینجا تنظیم نمی‌شود، چون توکن با کلید
             * symmetric داخلی اعتبارسنجی می‌شود و Issuer شما URL نیست.
             */
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,

                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(secretKey)),

                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,

                    ValidateAudience = true,
                    ValidAudience = jwtAudience,

                    ValidateLifetime = true,

                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    ClockSkew = TimeSpan.Zero
                };

            /*
             * JWT Authentication for SignalR
             */
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var requestPath =
                        context.HttpContext.Request.Path;

                    var isSignalRRequest =
                        requestPath.StartsWithSegments("/hub/Chat") ||
                        requestPath.StartsWithSegments("/hub/Notification") ||
                        requestPath.StartsWithSegments("/hub/Call");

                    if (!isSignalRRequest)
                    {
                        return Task.CompletedTask;
                    }

                    /*
                     * SignalR WebSocket معمولاً توکن را در query string
                     * با نام access_token ارسال می‌کند.
                     */
                    var accessToken =
                        context.Request.Query["access_token"]
                            .FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        var authorizationHeader =
                            context.Request.Headers.Authorization
                                .FirstOrDefault();

                        if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
                            authorizationHeader.StartsWith(
                                "Bearer ",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            accessToken =
                                authorizationHeader["Bearer ".Length..]
                                    .Trim();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        accessToken =
                            context.Request.Cookies["jwt"];
                    }

                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddMemoryCache();

    /*
     * JWT manager
     */
    builder.Services.AddScoped<IJwtManager, JwtManager>();

    /*
     * Repositories
     */
    builder.Services.AddScoped<IAuthRepository, AuthRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IChatRepository, ChatRepository>();
    builder.Services.AddScoped<IMessageRepository, MessageRepository>();
    builder.Services.AddScoped<IGroupRepository, GroupRepository>();
    builder.Services.AddScoped<ICallRepository, CallRepository>();

    builder.Services.AddScoped<
        IMediaStorageRepository,
        EncryptedLocalMediaStorageRepository>();

    builder.Services.AddScoped<
        IAppVersionPolicyRepository,
        AppVersionPolicyRepository>();

    /*
     * Services
     */
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IMessageService, MessageService>();
    builder.Services.AddScoped<IGroupService, GroupService>();
    builder.Services.AddScoped<ICallService, CallService>();

    builder.Services.AddScoped<
        IGenerativeAiService,
        GenerativeAiService>();

    builder.Services.AddScoped<
        INotificationService,
        FirebaseNotificationService>();

    builder.Services.AddHttpClient<
        IEmailService,
        RestEmailService>();

    builder.Services.AddScoped<KavenegarSmsOtpSender>();

    builder.Services.AddHttpClient<
        MelipayamakSmsOtpSender>();

    builder.Services.AddScoped<
        ISmsOtpSender,
        ConfigurableSmsOtpSender>();

    builder.Services.AddHttpClient();

    /*
     * AutoMapper
     */
    builder.Services.AddAutoMapper(
        config => { },
        typeof(MappingProfile));

    /*
     * SignalR
     */
    builder.Services.AddSingleton<
        IUserIdProvider,
        SubClaimUserIdProvider>();

    builder.Services.AddSingleton<
        IUserPresenceTracker,
        UserPresenceTracker>();

    var signalRBuilder =
        builder.Services
            .AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize =
                    3L * 1024 * 1024;

                options.EnableDetailedErrors = false;

                options.ClientTimeoutInterval =
                    TimeSpan.FromSeconds(120);

                options.HandshakeTimeout =
                    TimeSpan.FromSeconds(30);

                options.KeepAliveInterval =
                    TimeSpan.FromSeconds(15);

                options.StreamBufferCapacity = 20;
            })
            .AddJsonProtocol(options =>
            {
                options
                    .PayloadSerializerOptions
                    .ReferenceHandler =
                    ReferenceHandler.IgnoreCycles;
            });

    /*
     * Redis for SignalR
     */
    var redisEnabled =
        builder.Configuration.GetValue<bool>(
            "Redis:Enabled");

    var redisConnection =
        builder.Configuration[
            "Redis:ConnectionString"];

    if (string.IsNullOrWhiteSpace(redisConnection))
    {
        var redisHost =
            builder.Configuration[
                "Redis:Hosts:0:Host"];

        var redisPort =
            builder.Configuration[
                "Redis:Hosts:0:Port"]
            ?? "6379";

        if (!string.IsNullOrWhiteSpace(redisHost))
        {
            redisConnection =
                $"{redisHost}:{redisPort}," +
                "abortConnect=false," +
                "connectTimeout=5000," +
                "syncTimeout=5000";
        }
    }

    if (redisEnabled &&
        !string.IsNullOrWhiteSpace(redisConnection))
    {
        signalRBuilder.AddStackExchangeRedis(
            redisConnection,
            options =>
            {
                options.Configuration.ChannelPrefix =
                    StackExchange.Redis.RedisChannel.Literal(
                        "ChatNest");
            });
    }

    /*
     * Controllers
     */
    builder.Services.AddControllers();

    /*
     * CORS
     *
     * Origin، Header و Methodها مستقیماً از appsettings.json
     * خوانده می‌شوند.
     */
    var allowedOrigins =
        builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
        ?? Array.Empty<string>();

    var allowedHeaders =
        builder.Configuration
            .GetSection("Cors:AllowedHeaders")
            .Get<string[]>()
        ?? Array.Empty<string>();

    var allowedMethods =
        builder.Configuration
            .GetSection("Cors:AllowedMethods")
            .Get<string[]>()
        ?? Array.Empty<string>();

    var allowCredentials =
        builder.Configuration
            .GetValue<bool>(
                "Cors:AllowCredentials");

    /*
     * حذف فضای خالی، slash انتهایی و مقادیر تکراری.
     */
    allowedOrigins = allowedOrigins
        .Where(origin =>
            !string.IsNullOrWhiteSpace(origin))
        .Select(origin =>
            origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    allowedHeaders = allowedHeaders
        .Where(header =>
            !string.IsNullOrWhiteSpace(header))
        .Select(header => header.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    allowedMethods = allowedMethods
        .Where(method =>
            !string.IsNullOrWhiteSpace(method))
        .Select(method =>
            method.Trim().ToUpperInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (allowedOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins is empty.");
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "ChatNestCors",
            policy =>
            {
                /*
                 * Originهای مجاز
                 */
                policy.WithOrigins(allowedOrigins);

                policy.AllowAnyMethod();

                policy.AllowAnyHeader();



                if (allowCredentials)
                {
                    policy.AllowCredentials();
                }
            });
    });

    /*
     * Swagger
     */
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    /*
     * Database migration
     */
    try
    {
        Log.Information(
            "Applying database migrations");

        await app.Services.MigrateDatabaseAsync();

        Log.Information(
            "Database migrations applied");
    }
    catch (Exception ex)
    {
        Log.Error(
            ex,
            "Database migration failed, continuing without migration");
    }

    /*
     * Seed data
     */
    try
    {
        await app.Services.SeedIdentityDataAsync();
    }
    catch (Exception ex)
    {
        Log.Error(
            ex,
            "Seeding identity data failed, continuing without seeding");
    }

    /*
     * Middleware pipeline
     */

    /*
     * باید اولین middlewareهای برنامه باشد تا Scheme و IP واقعی
     * پیش از پردازش درخواست اصلاح شوند.
     */
    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging();

    /*
     * Exception handling
     */
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionFeature =
                    context.Features
                        .Get<IExceptionHandlerFeature>();

                var exception =
                    exceptionFeature?.Error;

                if (exception is not null)
                {
                    Log.Error(
                        exception,
                        "Unhandled exception for {Path}",
                        context.Request.Path);
                }

                context.Response.StatusCode =
                    exception switch
                    {
                        BadRequestException =>
                            StatusCodes.Status400BadRequest,

                        UnauthorizedAccessException =>
                            StatusCodes.Status401Unauthorized,

                        ForbiddenException =>
                            StatusCodes.Status403Forbidden,

                        NotFoundException =>
                            StatusCodes.Status404NotFound,

                        _ =>
                            StatusCodes.Status500InternalServerError
                    };

                context.Response.ContentType =
                    "application/json; charset=utf-8";

                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        message =
                            exception switch
                            {
                                BadRequestException
                                    or UnauthorizedAccessException
                                    or ForbiddenException
                                    or NotFoundException
                                    => exception.Message,

                                _ => "خطای داخلی سرور"
                            }
                    });
            });
        });
    }

    /*
     * HTTPS روی Nginx خاتمه پیدا می‌کند و Nginx درخواست را
     * به 127.0.0.1:5000 ارسال می‌کند.
     *
     * بنابراین این middleware ضروری نیست.
     */
    // app.UseHttpsRedirection();

    app.UseStaticFiles();

    /*
     * Routing باید قبل از CORS باشد.
     */
    app.UseRouting();

    /*
     * CORS باید بعد از Routing و قبل از Authentication و
     * Authorization اجرا شود.
     */
    app.UseCors("ChatNestCors");

    app.UseAuthentication();
    app.UseAuthorization();

    /*
     * Swagger
     */
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "ChatNest API v1");

        options.RoutePrefix = "swagger";
    });

    /*
     * Endpoints
     *
     * چون UseCors به صورت global اجرا شده، نیازی به
     * RequireCors روی تک‌تک endpointها نیست.
     */
    app.MapControllers();

    app.MapHub<ChatHub>("/hub/Chat");
    app.MapHub<CallHub>("/hub/Call");
    app.MapHub<NotificationHub>(
        "/hub/Notification");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(
        ex,
        "Application terminated unexpectedly");

    throw;
}
finally
{
    Log.CloseAndFlush();
}