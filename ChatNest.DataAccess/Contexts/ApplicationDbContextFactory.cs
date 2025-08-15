using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ChatNest.DataAccess.Contexts
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ChatNestDbContext>
    {
        public ChatNestDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ChatNestDbContext>();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

            return new ChatNestDbContext(optionsBuilder.Options);
        }
    }
}
