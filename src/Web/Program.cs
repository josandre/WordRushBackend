using Microsoft.EntityFrameworkCore;
using WordRush.Repository;

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
