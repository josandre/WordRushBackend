using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WordRush.Repository;
using WordRush.Web.Features.Game;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
  .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Web"))
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .AddEnvironmentVariables();

Console.WriteLine($"Starting migrations in {builder.Environment.EnvironmentName}");

if (builder.Environment.IsDevelopment())
{
  // Using a dummy class to access Web User Secrets and avoid secrets duplications
  builder.Configuration.AddUserSecrets(typeof(GameController).Assembly, optional: true);
}

var services = builder.Services;

var connectionString = builder.Configuration.GetConnectionString("WordRushDb");

services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
  connectionString,
  x => x.MigrationsAssembly("WordRush.Migrations")));

IServiceProvider provider = services.BuildServiceProvider();

using (var context = (AppDbContext)provider.GetService(typeof(AppDbContext)))
{
  context?.Database.Migrate();
}

Console.WriteLine("Migrations successfully applied");
