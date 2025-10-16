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
if (args.Length == 1 && args[0].ToLower() == "seeddata")
{
    try
    {
        SeedData(app);
        Console.WriteLine("Seeding completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seeding error: {ex.Message}");
        Exception? ie = ex.InnerException;
        int depth = 0;
        while (ie != null && depth < 5)
        {
            Console.WriteLine($"Inner[{depth}]: {ie.GetType().FullName}: {ie.Message}");
            ie = ie.InnerException;
            depth++;
        }
        Console.WriteLine(ex.ToString());
    }
    return;
}

app.ConfigureSwagger();
app.ConfigureExceptionHandler();

// Force 200 for any 4xx/5xx responses, wrapping payload
// Place before auth so 401/403 are wrapped too
app.UseAlways200ResponseWrapper();

app.UseCors("AllowSpecificOrigins");
app.ConfigureStaticFiles();
app.UseAuthentication();

app.UseAuthorization();

app.UseAuditLogging(options =>
{
    options.CaptureRequestBody = true;
    options.CaptureResponseBody = true;
    options.MaxBodyChars = 4000;
    options.SkipPathStartsWith = new[] { "/swagger", "/notificationHub" };
    options.EnrichExtras = ctx => new
    {
        TraceId = ctx.TraceIdentifier,
        Endpoint = ctx.GetEndpoint()?.DisplayName
    };
});

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
