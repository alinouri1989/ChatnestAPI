using ChatNest.Entities.Identity;
using ChatNest.Entities.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChatNest.DataAccess.Contexts
{
    public class ChatNestDbContext : IdentityDbContext<User, Role, string,
               UserClaim, UserRole, UserLogin,
               RoleClaim, UserToken>
    {
        public ChatNestDbContext(DbContextOptions<ChatNestDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Call> Calls { get; set; }
        public DbSet<AppVersionPolicy> AppVersionPolicies { get; set; }
        public DbSet<AppVersionReleaseNote> AppVersionReleaseNotes { get; set; }

        // Add junction table DbSets
        public DbSet<CallParticipant> CallParticipants { get; set; }
        public DbSet<ChatParticipant> ChatParticipants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Identity tables
            string schema = "idf";

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable(name: "Roles", schema: schema);
            });

            modelBuilder.Entity<UserClaim>(entity =>
            {
                entity.Property(e => e.ClaimType).HasMaxLength(50);
                entity.Property(e => e.ClaimValue).HasMaxLength(50);
                entity.ToTable("UserClaims", schema);
            });

            modelBuilder.Entity<UserLogin>(entity =>
            {
                entity.ToTable("UserLogins", schema);
            });

            modelBuilder.Entity<RoleClaim>(entity =>
            {
                entity.ToTable("RoleClaims", schema);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("UserRoles", schema);
            });

            modelBuilder.Entity<UserToken>(entity =>
            {
                entity.ToTable("UserTokens", schema);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens", schema);
                entity.HasKey(c => c.Token);

                entity.HasOne(c => c.User)
                      .WithMany(w => w.RefreshTokens)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired();
            });

            // Configure application entities
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserIdentifier).HasMaxLength(30);
                entity.Property(e => e.PhoneNumber).HasMaxLength(15);
                entity.Property(e => e.Biography).HasMaxLength(500);
                entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FcmTokensJson).HasColumnType("nvarchar(max)");
                entity.HasIndex(e => e.UserIdentifier)
                      .IsUnique()
                      .HasFilter("[UserIdentifier] IS NOT NULL");

                entity.Property(e => e.ProfilePhoto)
                      .HasConversion(
                          v => v == null ? null : v.ToString(),
                          v => string.IsNullOrWhiteSpace(v) ? null : new Uri(v));

                entity.HasMany(u => u.CreatedGroups)
                      .WithOne(g => g.Creator)
                      .HasForeignKey(g => g.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Chat>(entity =>
            {
                entity.ToTable("Chats");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChatType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ArchivedForJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.PinnedForJson).HasColumnType("nvarchar(max)");

                entity.HasMany(c => c.Messages)
                      .WithOne(m => m.Chat)
                      .HasForeignKey(m => m.ChatId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with ChatParticipants
                entity.HasMany(c => c.ChatParticipants)
                      .WithOne(cp => cp.Chat)
                      .HasForeignKey(cp => cp.ChatId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("Messages");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.ThumbnailUrl);
                entity.Property(e => e.FileName).HasMaxLength(255);
                entity.Property(e => e.ReplyToFileName).HasMaxLength(255);
                entity.Property(e => e.SenderId).IsRequired();
                entity.Property(e => e.StatusJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.DeletedForJson).HasColumnType("nvarchar(max)");

                entity.HasOne(m => m.Sender)
                      .WithMany()
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Group>(entity =>
            {
                entity.ToTable("Groups");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.Kind).HasDefaultValue(ChatNest.Entities.Enums.GroupKind.Group);
                entity.Property(e => e.ParticipantsJson).HasColumnType("nvarchar(max)");

                entity.Property(e => e.Photo)
                      .HasConversion(
                          v => v == null ? null : v.ToString(),
                          v => string.IsNullOrWhiteSpace(v) ? null : new Uri(v));

            });

            modelBuilder.Entity<Call>(entity =>
            {
                entity.ToTable("Calls");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DeletedForJson).HasColumnType("nvarchar(max)");

                entity.HasOne(c => c.Chat)
                      .WithMany()
                      .HasForeignKey(c => c.ChatId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Configure relationship with CallParticipants
                entity.HasMany(c => c.CallParticipants)
                      .WithOne(cp => cp.Call)
                      .HasForeignKey(cp => cp.CallId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AppVersionPolicy>(entity =>
            {
                entity.ToTable("AppVersionPolicies");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Platform).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Channel).IsRequired().HasMaxLength(30);
                entity.Property(e => e.LatestVersion).IsRequired().HasMaxLength(30);
                entity.Property(e => e.MinimumSupportedVersion).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.StoreUrl).IsRequired().HasMaxLength(2048);
                entity.HasIndex(e => new { e.Platform, e.Channel }).IsUnique();

                entity.HasMany(e => e.ReleaseNotes)
                      .WithOne(e => e.AppVersionPolicy)
                      .HasForeignKey(e => e.AppVersionPolicyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasData(
                    CreateVersionPolicy(1, "android", "https://play.google.com/store/apps/details?id=ir.chatnest.app"),
                    CreateVersionPolicy(2, "ios", "https://apps.apple.com/app/chatnest/id0000000000"),
                    CreateVersionPolicy(
                        3,
                        "pwa",
                        "https://app.chatnest.ir",
                        latestVersion: "2.6.1",
                        latestBuild: 132,
                        title: "نسخه جدید چت‌نست آماده است",
                        message: "برای استفاده از آخرین بهبودها و رفع اشکال‌ها، برنامه را به‌روزرسانی کنید."));
            });

            modelBuilder.Entity<AppVersionReleaseNote>(entity =>
            {
                entity.ToTable("AppVersionReleaseNotes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Text).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => new { e.AppVersionPolicyId, e.DisplayOrder }).IsUnique();
                entity.HasData(
                    new AppVersionReleaseNote { Id = 1, AppVersionPolicyId = 1, DisplayOrder = 1, Text = "Improved chat performance" },
                    new AppVersionReleaseNote { Id = 2, AppVersionPolicyId = 1, DisplayOrder = 2, Text = "Fixed notification problems" },
                    new AppVersionReleaseNote { Id = 3, AppVersionPolicyId = 1, DisplayOrder = 3, Text = "Improved application security" },
                    new AppVersionReleaseNote { Id = 4, AppVersionPolicyId = 2, DisplayOrder = 1, Text = "Improved chat performance" },
                    new AppVersionReleaseNote { Id = 5, AppVersionPolicyId = 3, DisplayOrder = 1, Text = "Improved chat performance" });
            });

            // Configure CallParticipant junction table
            modelBuilder.Entity<CallParticipant>(entity =>
            {
                entity.ToTable("CallParticipants");

                // Composite primary key
                entity.HasKey(cp => new { cp.CallId, cp.UserId });

                // Configure relationships
                entity.HasOne(cp => cp.Call)
                      .WithMany(c => c.CallParticipants)
                      .HasForeignKey(cp => cp.CallId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.User)
                      .WithMany() // No navigation property back to User
                      .HasForeignKey(cp => cp.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure properties
                entity.Property(cp => cp.UserId).IsRequired().HasMaxLength(450);
                entity.Property(cp => cp.JoinedAt).IsRequired();

                // Add index for better query performance
                entity.HasIndex(cp => cp.UserId).HasDatabaseName("IX_CallParticipants_UserId");
                entity.HasIndex(cp => cp.CallId).HasDatabaseName("IX_CallParticipants_CallId");
            });

            // Configure ChatParticipant junction table
            modelBuilder.Entity<ChatParticipant>(entity =>
            {
                entity.ToTable("ChatParticipants");

                // Composite primary key
                entity.HasKey(cp => new { cp.ChatId, cp.UserId });

                // Configure relationships
                entity.HasOne(cp => cp.Chat)
                      .WithMany(c => c.ChatParticipants)
                      .HasForeignKey(cp => cp.ChatId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cp => cp.User)
                      .WithMany() // No navigation property back to User
                      .HasForeignKey(cp => cp.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure properties
                entity.Property(cp => cp.UserId).IsRequired().HasMaxLength(450);
                entity.Property(cp => cp.JoinedAt).IsRequired();

                // Add index for better query performance
                entity.HasIndex(cp => cp.UserId).HasDatabaseName("IX_ChatParticipants_UserId");
                entity.HasIndex(cp => cp.ChatId).HasDatabaseName("IX_ChatParticipants_ChatId");
            });
        }

        public async Task SaveWithConcurrencyRetryAsync()
        {
            try
            {
                await SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // رکورد تغییر کرده/حذف شده
                foreach (var entry in ex.Entries)
                {
                    // اگر رکورد حذف شده باشد:
                    var databaseValues = await entry.GetDatabaseValuesAsync();
                    if (databaseValues == null)
                    {
                        throw new NotFoundException("Entity was deleted by another operation.");
                    }

                    // رکورد هنوز هست، مقادیر دیتابیس را می‌گیریم و روی entry می‌ریزیم
                    entry.OriginalValues.SetValues(databaseValues);
                }

                // دوباره تلاش
                await SaveChangesAsync();
            }
        }

        private static AppVersionPolicy CreateVersionPolicy(
            int id,
            string platform,
            string storeUrl,
            string latestVersion = "2.5.0",
            int latestBuild = 130,
            string? title = null,
            string? message = null) => new()
        {
            Id = id,
            Platform = platform,
            Channel = "production",
            LatestVersion = latestVersion,
            LatestBuild = latestBuild,
            MinimumSupportedVersion = "2.3.0",
            MinimumSupportedBuild = 110,
            Maintenance = false,
            Title = title ?? "A new ChatNest version is available",
            Message = message ?? "Update ChatNest to get the latest improvements and fixes.",
            StoreUrl = storeUrl,
            RemindAfterSeconds = 86400
        };
    }
}
