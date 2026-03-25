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
using ChatNest.Services.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
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

    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
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
            shared: true));
    FFmpeg.SetExecutablesPath(builder.Configuration["FileStorage:PathFFmpeg"]);

    // Add DbContext
    builder.Services.AddDbContext<ChatNestDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Add Identity
    builder.Services.AddIdentity<User, Role>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
        options.Lockout.MaxFailedAccessAttempts = 5;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
                .AddRoles<Role>()
                .AddRoleManager<RoleManager<Role>>()
                .AddRoleValidator<RoleValidator<Role>>()
                .AddEntityFrameworkStores<ChatNestDbContext>()
                .AddDefaultTokenProviders();

    // Configure encrypted local file storage
    builder.Services.AddSingleton<FileStorageConfig>(provider =>
        new FileStorageConfig(builder.Configuration));

    // Configure AI Services
    builder.Services.AddSingleton<GeminiConfig>(provider =>
        new GeminiConfig(builder.Configuration));

    builder.Services.AddSingleton<HuggingFaceConfig>(provider =>
        new HuggingFaceConfig(builder.Configuration));

    // Add JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("JWT");
    var secretKey = jwtSettings["SecretKey"] ?? "a154b25da06306c08587fc94866f9854";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false;
        options.Authority = jwtSettings["Issuer"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "chatnest-api",
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"] ?? "chatnest-clients",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Configure JWT authentication for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // Check for token in multiple locations
                if (string.IsNullOrEmpty(accessToken))
                {
                    // Try Authorization header
                    accessToken = context.Request.Headers["Authorization"]
                        .FirstOrDefault()?.Split(" ").Last();
                }

                if (string.IsNullOrEmpty(accessToken))
                {
                    // Try cookie
                    accessToken = context.Request.Cookies["jwt"];
                }

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hub/Chat") ||
                     path.StartsWithSegments("/hub/Notification") ||
                     path.StartsWithSegments("/hub/Call")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    // Add Authorization
    builder.Services.AddAuthorization();

    // Register JWT Manager
    builder.Services.AddScoped<IJwtManager, JwtManager>();

    // Register Repositories
    builder.Services.AddScoped<IAuthRepository, AuthRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IChatRepository, ChatRepository>();
    builder.Services.AddScoped<IMessageRepository, MessageRepository>();
    builder.Services.AddScoped<IGroupRepository, GroupRepository>();
    builder.Services.AddScoped<ICallRepository, CallRepository>();
    builder.Services.AddScoped<IMediaStorageRepository, EncryptedLocalMediaStorageRepository>();

    // Register Services
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IMessageService, MessageService>();
    builder.Services.AddScoped<IGroupService, GroupService>();
    builder.Services.AddScoped<ICallService, CallService>();
    builder.Services.AddScoped<IGenerativeAiService, GenerativeAiService>();

    // Add HttpClient for AI services
    builder.Services.AddHttpClient();

    // Add AutoMapper
    builder.Services.AddAutoMapper(typeof(MappingProfile));
    builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
    builder.Services.AddSingleton<IUserPresenceTracker, UserPresenceTracker>();

    var signalRBuilder = builder.Services.AddSignalR(options =>
    {
        // Keep SignalR payloads small; file uploads are chunked.
        options.MaximumReceiveMessageSize = 2L * 1024 * 1024;
        options.EnableDetailedErrors = false;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.StreamBufferCapacity = 20;
    })
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

    var redisEnabled = builder.Configuration.GetValue<bool>("Redis:Enabled");
    var redisConnection = builder.Configuration["Redis:ConnectionString"];
    if (string.IsNullOrWhiteSpace(redisConnection))
    {
        var redisHost = builder.Configuration["Redis:Hosts:0:Host"];
        var redisPort = builder.Configuration["Redis:Hosts:0:Port"] ?? "6379";
        if (!string.IsNullOrWhiteSpace(redisHost))
        {
            redisConnection = $"{redisHost}:{redisPort},abortConnect=false,connectTimeout=5000,syncTimeout=5000";
        }
    }

    if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnection))
    {
        signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("ChatNest");
        });
    }


    // Add Controllers
    builder.Services.AddControllers();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    var allowedMethods = builder.Configuration.GetSection("Cors:AllowedMethods").Get<string[]>() ?? Array.Empty<string>();
    var allowedHeaders = builder.Configuration.GetSection("Cors:AllowedHeaders").Get<string[]>() ?? Array.Empty<string>();
    var allowCredentials = builder.Configuration.GetValue<bool>("Cors:AllowCredentials");

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("_myAllowSpecificOrigins", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .WithHeaders(allowedHeaders.Length > 0 ? allowedHeaders : ["Content-Type", "Authorization"])
                  .SetPreflightMaxAge(TimeSpan.FromHours(1))
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
            if (allowCredentials) policy.AllowCredentials();

        });
    });

    // Add Swagger for development
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    var app = builder.Build();

    await app.Services.SeedIdentityDataAsync();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatNest API v1");
        c.RoutePrefix = "swagger";
    });

    // Configure pipeline
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
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (exceptionFeature?.Error is not null)
                {
                    Log.Error(exceptionFeature.Error, "Unhandled exception for {Path}", context.Request.Path);
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "خطای داخلی سرور" });
            });
        });
    }

    //app.UseHttpsRedirection();
    //app.UseStaticFiles();
    //app.UseRouting();
    //app.UseCors("_myAllowSpecificOrigins");
    //app.UseAuthentication();
    //app.UseAuthorization();

    //app.MapControllers();

    //app.MapHub<ChatHub>("hub/Chat");
    //app.MapHub<CallHub>("hub/Call");
    //app.MapHub<NotificationHub>("hub/Notification");

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseCors("_myAllowSpecificOrigins");  // ✅ بعد از Routing

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers().RequireCors("_myAllowSpecificOrigins");

    app.MapHub<ChatHub>("/hub/Chat").RequireCors("_myAllowSpecificOrigins");
    app.MapHub<CallHub>("/hub/Call").RequireCors("_myAllowSpecificOrigins");
    app.MapHub<NotificationHub>("/hub/Notification").RequireCors("_myAllowSpecificOrigins");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
