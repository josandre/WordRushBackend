using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Repository;
using WordRush.Repository.Models;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddHealthChecks();
services.AddControllers();
services.AddEndpointsApiExplorer();

services.AddSwaggerGen(options =>
{
  var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
  options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var myAllowSpecificOrigins = "_myAllowSpecificOrigins";

services.AddCors(options =>
{
  options.AddPolicy(
    myAllowSpecificOrigins,
    policy => { policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod(); });
});

if (builder.Environment.IsDevelopment())
{
  builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

var connection = builder.Configuration.GetConnectionString("WordRushDb");

services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));


builder.Services.AddDataProtection();

var identityBuilder = builder.Services
  .AddIdentityCore<User>(
    o =>
    {
      o.Password.RequireDigit = true;
      o.Password.RequiredLength = 8;
      o.Password.RequireLowercase = true;
      o.Password.RequireUppercase = true;
      o.Password.RequireNonAlphanumeric = false;

      o.User.AllowedUserNameCharacters = null!;
      o.SignIn.RequireConfirmedEmail = false;
    });

identityBuilder
  .AddRoles<Role>()
  .AddEntityFrameworkStores<AppDbContext>()
  .AddDefaultTokenProviders();

builder.Services
  .Configure<PasswordHasherOptions>(o => o.IterationCount = 30_000)
  .AddHttpContextAccessor()
  .AddScoped<SignInManager<User>, SignInManager<User>>();

builder.Services
  .AddScoped<IAuthService, AuthService>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseCors(myAllowSpecificOrigins);
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");

await app.RunAsync();
