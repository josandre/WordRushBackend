using Microsoft.EntityFrameworkCore;
using WordRush.Repository;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;

services.AddHealthChecks();
services.AddControllers();
services.AddEndpointsApiExplorer();

services.AddSwaggerGen(options =>
{
  string xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
  options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

string myAllowSpecificOrigins = "_myAllowSpecificOrigins";

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

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
  _ = app.UseSwagger();
  _ = app.UseSwaggerUI();
}

app.UseCors(myAllowSpecificOrigins);
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");

await app.RunAsync();
