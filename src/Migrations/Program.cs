using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WordRush.Repository;
using WordRush.Web.Features.Game;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Configuration
  .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Web"))
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .AddEnvironmentVariables();

Console.WriteLine($"Starting migrations in {builder.Environment.EnvironmentName}");

if (builder.Environment.IsDevelopment())
{
  // Using a dummy class to access Web User Secrets and avoid secrets duplications
  _ = builder.Configuration.AddUserSecrets(typeof(GameController).Assembly, optional: true);
}

IServiceCollection services = builder.Services;

string? connectionString = builder.Configuration.GetConnectionString("WordRushDb");

services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
  connectionString,
  x => x.MigrationsAssembly("WordRush.Migrations")));

ServiceProvider provider = services.BuildServiceProvider();

using (AppDbContext context = (AppDbContext)provider.GetService(typeof(AppDbContext))!)
{
  context?.Database.Migrate();
}

Console.WriteLine("Migrations successfully applied");
