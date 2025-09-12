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
                entity.Property(e => e.PhoneNumber).HasMaxLength(15);
                entity.Property(e => e.Biography).HasMaxLength(500);
                entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(100);

                entity.Property(e => e.ProfilePhoto)
                      .HasConversion(
                          v => v.ToString(),
                          v => new Uri(v));

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
                entity.Property(e => e.FileName).HasMaxLength(255);
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
                entity.Property(e => e.ParticipantsJson).HasColumnType("nvarchar(max)");

                entity.Property(e => e.Photo)
                      .HasConversion(
                          v => v!.ToString(),
                          v => new Uri(v));
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
    }
}