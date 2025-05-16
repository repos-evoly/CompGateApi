using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace CompGateApi.Core.Startup
{
  public static class SwaggerConfiguration
  {
    public static WebApplication ConfigureSwagger(this WebApplication app)
    {
      if (app.Environment.IsDevelopment() || app.Environment.IsProduction() || app.Environment.IsStaging())
      {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
          c.RoutePrefix = string.Empty;
          string swaggerJsonBasePath = string.IsNullOrWhiteSpace(c.RoutePrefix) ? "." : "..";
          c.SwaggerEndpoint($"{swaggerJsonBasePath}/swagger/v1/swagger.json", "Blocking API");
        });
      }
      return app;
    }

  }

}