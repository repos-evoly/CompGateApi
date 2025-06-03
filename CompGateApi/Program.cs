using CompGateApi.Core.Startup;
using CompGateApi.Data.Seeding;
using CompGateApi.Extensions;
using CompGateApi.Hubs;


var builder = WebApplication.CreateBuilder(args);
builder.RegisterServices();
builder.Logging.ClearProviders();
builder.Host.ConfigureSerilog();




var app = builder.Build();

var env = app.Services.GetRequiredService<IHostEnvironment>();
Console.WriteLine($"Environment: {env.EnvironmentName}");

// to run data seeding use on terminal the command "dotnet run seeddata"
if (args.Length == 1 && args[0].ToLower() == "seeddata") SeedData(app);

app.ConfigureSwagger();
app.ConfigureExceptionHandler();



app.UseCors("AllowSpecificOrigins");
app.ConfigureStaticFiles();
app.UseAuthentication();

app.UseAuthorization();

app.MapHub<NotificationHub>("/notificationHub");


app.RegisterEndpoints();



app.Run();

static void SeedData(IHost app)
{
    var scopedFactory = app.Services.GetService<IServiceScopeFactory>()
        ?? throw new InvalidOperationException("ServiceScopeFactory not found.");

    using var scope = scopedFactory.CreateScope();
    var service = scope.ServiceProvider.GetService<DataSeeder>()
        ?? throw new InvalidOperationException("DataSeeder service not found.");

    service.Seed();
}