using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using WordRush.Core.Features;
using WordRush.Core.Features.Game;
using WordRush.Core.Features.Game.CategoryColumns;
using WordRush.Core.Features.Game.CategoryTypes;
using WordRush.Core.Features.Realtime;
using WordRush.Core.Features.Scoring;
using WordRush.Core.Features.Settings;
using WordRush.Core.Features.Hints;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Repository;
using WordRush.Repository.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;

// ------------------------------------------------------------
// Core Service Setup
// ------------------------------------------------------------
services.AddHealthChecks();
services.AddControllers()
    .AddJsonOptions(options =>
    {
      _ = options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
      options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
      _ = options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
      _ = options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
services.AddEndpointsApiExplorer();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

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
            Array.Empty<string>()
        }
    });
});

// ------------------------------------------------------------
// Authentication & Identity
// ------------------------------------------------------------
string? jwtKey = builder.Configuration["Jwt:Secret"];
string? jwtIssuer = builder.Configuration["Jwt:Issuer"];

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
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
  };
});

// ------------------------------------------------------------
// CORS Setup
// ------------------------------------------------------------
string myAllowSpecificOrigins = "_myAllowSpecificOrigins";
services.AddCors(options =>
{
  options.AddPolicy(myAllowSpecificOrigins, policy =>
  {
    _ = policy.AllowAnyOrigin()
          .AllowAnyHeader()
          .AllowAnyMethod();
  });
});

// ------------------------------------------------------------
// Development Secrets & Database
// ------------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
  _ = builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

string? connection = builder.Configuration.GetConnectionString("WordRushDb");
services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddDataProtection();

// ------------------------------------------------------------
// Identity Configuration
// ------------------------------------------------------------
IdentityBuilder identityBuilder = builder.Services
    .AddIdentityCore<User>(options =>
    {
      options.Password.RequireDigit = true;
      options.Password.RequiredLength = 8;
      options.Password.RequireLowercase = true;
      options.Password.RequireUppercase = true;
      options.Password.RequireNonAlphanumeric = false;

      options.User.AllowedUserNameCharacters = null!;
      options.SignIn.RequireConfirmedEmail = false;
    });

identityBuilder
    .AddRoles<Role>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .Configure<PasswordHasherOptions>(o => o.IterationCount = 300_000)
    .AddHttpContextAccessor()
    .AddScoped<SignInManager<User>, SignInManager<User>>();

builder.Services.AddHttpClient();

builder.Services
    .AddScoped<IAuthService, AuthService>()
    .AddScoped<IRoleService, RoleService>()
    .AddScoped<IUserService, UserService>()
    .AddSingleton<IFeatureFlagService, FeatureFlagService>()
    .AddSingleton<IWordRushWebSocketService, WordRushWebSocketService>()
    .AddScoped<IGameStatisticsService, GameStatisticsService>()
    .AddScoped<IScoringService, StopGameScoringService>()
    .AddScoped<IGameSettingsService, GameSettingsService>()
    .AddScoped<ICategoryColumns, CategoryColumnsService>()
    .AddScoped<ICategoryTypes, CategoryTypesService>()
    // Register hint service for AI-powered hint generation
    .AddScoped<IHintService, HintService>()
    .AddScoped<ICategoryValidationService, CategoryValidationService>();

// Register administrative services
builder.Services.AddScoped<WordRush.Core.Features.Admin.IAdminService, WordRush.Core.Features.Admin.AdminService>();

builder.Host.UseSerilog();

// ------------------------------------------------------------
// Build Application
// ------------------------------------------------------------
WebApplication app = builder.Build();

// Start warm-up in background after 5 s (GPU ready)
_ = Task.Run(async () =>
{
  await Task.Delay(10000);
  try
  {
    using IServiceScope scope = app.Services.CreateScope();
    if (scope.ServiceProvider.GetRequiredService<IScoringService>() is StopGameScoringService scoring)
    {
      Log.Information("Starting delayed Ollama GPU warm-up...");
      await scoring.WarmUpModelAsync();
    }
  }
  catch (Exception ex)
  {
    Log.Warning(ex, "Ollama warm-up skipped — model server may not be ready.");
  }
});

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

// ------------------------------------------------------------
// WebSocket Integration
// ------------------------------------------------------------
app.UseWebSockets(new WebSocketOptions
{
  KeepAliveInterval = TimeSpan.FromSeconds(120)
});

// ------------------------------------------------------------
// Run Application
// ------------------------------------------------------------
await app.RunAsync();
