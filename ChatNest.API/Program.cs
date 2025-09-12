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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// Configure Cloudinary
builder.Services.AddSingleton<CloudinaryConfig>(provider =>
    new CloudinaryConfig(builder.Configuration));

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
builder.Services.AddScoped<ICloudRepository, CloudRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
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

// Add SignalR
builder.Services.AddSignalR();

// Add Controllers
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("https://localhost:5173", "http://localhost:3000", "https://localhost:3000") // Add your frontend URLs
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Important for SignalR
    });
});

// Add Swagger for development
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
await builder.Services.SeedIdentityDataAsync();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("hub/Chat");
app.MapHub<CallHub>("hub/Call");
app.MapHub<NotificationHub>("hub/Notification");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ChatNestDbContext>();
    context.Database.Migrate();
}

app.Run();