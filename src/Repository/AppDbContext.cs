using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WordRush.Repository.Models;

namespace WordRush.Repository
{
  public class AppDbContext : DbContext
  {
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<CategoryColumn> CategoryColumns { get; set; }

    public DbSet<CategoryType> CategoryTypes { get; set; }

    public DbSet<Privilege> Privileges { get; set; }

    public DbSet<User> Users { get; set; }

    public DbSet<Role> Roles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      if (!optionsBuilder.IsConfigured)
      {
        IConfigurationRoot configuration = new ConfigurationBuilder()
          .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Web"))
          .AddJsonFile("appsettings.json")
          .Build();

        string? connectionString = configuration.GetConnectionString("WordRushDb");

        _ = optionsBuilder.UseNpgsql(
          connectionString,
          x => x.MigrationsAssembly("WordRush.Migrations"));
      }
    }
  }
}
