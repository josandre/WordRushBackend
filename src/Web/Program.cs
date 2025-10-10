using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using WordRush.Core.Features;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Repository;
using WordRush.Repository.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;

services.AddHealthChecks();
services.AddControllers();
services.AddEndpointsApiExplorer();

Log.Logger = new LoggerConfiguration()
  .ReadFrom.Configuration(builder.Configuration).CreateLogger();

services.AddSwaggerGen(options =>
{
  string xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
  options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

  options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Enter 'Bearer' [space] and then your valid token.\n\nExample: \"Bearer eyJhbGciOiJI...\""
  });

  options.AddSecurityRequirement(new OpenApiSecurityRequirement
  {
    {
      new OpenApiSecurityScheme
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.SecurityScheme,
          Id = "Bearer"
        }
      },
      []
    }
  });
});

var jwtKey = builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];

services.AddAuthentication(options =>
  {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
  })
  .AddJwtBearer(options =>
  {
    options.TokenValidationParameters = new TokenValidationParameters
    {
      ValidateIssuer = true,
      ValidateAudience = true,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,

      ValidIssuer = jwtIssuer,
      ValidAudience = jwtIssuer,
      IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
  });

var myAllowSpecificOrigins = "_myAllowSpecificOrigins";

services.AddCors(options =>
{
  options.AddPolicy(
    myAllowSpecificOrigins,
    policy => { _ = policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod(); });
});

if (builder.Environment.IsDevelopment())
{
  _ = builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

string? connection = builder.Configuration.GetConnectionString("WordRushDb");

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
  .Configure<PasswordHasherOptions>(o => o.IterationCount = 300_000)
  .AddHttpContextAccessor()
  .AddScoped<SignInManager<User>, SignInManager<User>>();

builder.Services
  .AddScoped<IAuthService, AuthService>()
  .AddScoped<IRoleService, RoleService>()
  .AddScoped<IUserService, UserService>()
  .AddScoped<IProfileService, ProfileService>();

builder.Services.AddDataProtection();

builder.Host.UseSerilog();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  _ = app.UseSwagger();
  _ = app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseCors(myAllowSpecificOrigins);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
