using ChatNest.Entities.Identity;
using ChatNest.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChatNest.DataAccess.Contexts
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task SeedIdentityDataAsync(this IServiceCollection app)
        {
            using var scope = app.BuildServiceProvider().CreateScope();
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
                    UserName = "Admin",
                    NormalizedUserName = "ADMIN",
                    Email = "alinouri1989@gmail.com",
                    NormalizedEmail = "ALINOURI1989@GMAIL.COM",
                    LockoutEnabled = false,
                    MobileNo = "09217579859",
                    MobileConfirmed = true,
                    Firstname = "علی",
                    Lastname = "نوری",
                    PasswordHash = passwordHasher.HashPassword(null,"Aa123456")
                },new User {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "SpecialSupport",
                    NormalizedUserName = "SPECIALSUPPORT",
                    Email = "SpecialSupport@gmail.com",
                    NormalizedEmail = "SPECIALSUPPORT@GMAIL.COM",
                    LockoutEnabled = false,
                    Firstname = "پشتیبان خاص",
                    Lastname = "اول",
                    MobileNo = "09217579859",
                    MobileConfirmed = true,
                    PasswordHash = passwordHasher.HashPassword(null,"Aa123456")
                },new User {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "Support",
                    NormalizedUserName = "SUPPORT",
                    Email = "Support@gmail.com",
                    NormalizedEmail = "SUPPORT@GMAIL.COM",
                    LockoutEnabled = false,
                    Firstname = "پشتیبان",
                    Lastname = "اول",
                    MobileNo= "09352408400",
                    MobileConfirmed = true,
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

