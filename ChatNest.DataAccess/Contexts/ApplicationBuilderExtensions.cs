using ChatNest.Entities.Identity;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChatNest.DataAccess.Contexts
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task SeedIdentityDataAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatNestDbContext>();
            var users = new List<User>();
            var roles = new List<Role>() { new Role {Id= Guid.NewGuid().ToString(),Name = "Admin", NormalizedName = "Admin", ConcurrencyStamp = Guid.NewGuid().ToString() },
                    new Role {Id= Guid.NewGuid().ToString(), Name = "SpecialSupport", NormalizedName = "SpecialSupport", ConcurrencyStamp = Guid.NewGuid().ToString() },
                    new Role {Id= Guid.NewGuid().ToString(), Name = "Support", NormalizedName = "Support", ConcurrencyStamp = Guid.NewGuid().ToString() },
                    new Role {Id= Guid.NewGuid().ToString(), Name = "User", NormalizedName = "User", ConcurrencyStamp = Guid.NewGuid().ToString() } };
            await db.Database.MigrateAsync();

            // Seed roles  
            if (!await db.Roles.AnyAsync())
            {
                db.Roles.AddRange(roles);

                await db.SaveChangesAsync();
            }

            // Seed user  
            if (!await db.Users.AnyAsync())
            {
                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();

                users = new List<User>() {
                new User {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "alinouri22",
                    NormalizedUserName = "alinouri22",
                    Email = "alinouri22@yahoo.com",
                    NormalizedEmail = "alinouri22@yahoo.com",
                    LockoutEnabled = false,
                    MobileNo = "09217579859",
                    MobileConfirmed = true,
                    Firstname = "علی",
                    Lastname = "نوری",
                    PasswordHash = passwordHasher.HashPassword(null,"Aa123456")
                }};

                db.Users.AddRange(users);
                await db.SaveChangesAsync();
            }

            // Seed user-role link  
            if (!await db.UserRoles.AnyAsync())
            {
                db.UserRoles.Add(new UserRole { UserId = users[0].Id, RoleId = roles[0].Id });
                db.UserRoles.Add(new UserRole { UserId = users[1].Id, RoleId = roles[1].Id });
                db.UserRoles.Add(new UserRole { UserId = users[2].Id, RoleId = roles[2].Id });
                await db.SaveChangesAsync();
            }
        }
    }
}

