using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WordRush.Repository.Models;

namespace WordRush.Repository;

public class AppDbContext : DbContext
{
  public AppDbContext()
  {
  }

  public AppDbContext(DbContextOptions<AppDbContext> options)
    : base(options)
  {
  }

  public DbSet<Game> Games { get; set; }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    if (!optionsBuilder.IsConfigured)
    {
      var configuration = new ConfigurationBuilder()
        .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Web"))
        .AddJsonFile("appsettings.json")
        .Build();

      var connectionString = configuration.GetConnectionString("WordRushDb");

      optionsBuilder.UseNpgsql(
        connectionString,
        x => x.MigrationsAssembly("WordRush.Migrations"));
    }
  }
}
