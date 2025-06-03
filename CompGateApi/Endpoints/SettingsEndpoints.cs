using CompGateApi.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CompGateApi.Data.Abstractions;
using AutoMapper;
using CompGateApi.Data.Models;
using CompGateApi.Abstractions;

namespace CompGateApi.Endpoints
{
    public class SettingsEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var settings = app.MapGroup("/api/settings").RequireAuthorization("requireAuthUser");

            settings.MapGet("/", Get)
                .WithName("GetSettings")
                .Produces<SettingsDto>(200);

            settings.MapPatch("/", Patch) // Use PATCH for partial update
                .WithName("UpdateSettings")
                .Accepts<SettingsPatchDto>("application/json")
                .Produces<SettingsDto>(200)
                .Produces(400);
        }

        // 🔹 GET the first Settings (not by ID)
        public static async Task<IResult> Get([FromServices] ISettingsRepository settingsRepository, [FromServices] IMapper mapper)
        {
            var settings = await settingsRepository.GetFirstSettingsAsync(); // Fetch the first row
            if (settings == null) return TypedResults.NotFound("Settings not found.");

            return TypedResults.Ok(mapper.Map<SettingsDto>(settings));
        }
        // 🔹 PATCH to update Settings
        public static async Task<IResult> Patch([FromServices] ISettingsRepository settingsRepository, [FromServices] IMapper mapper, [FromBody] SettingsPatchDto settingsDto)
        {
            var settings = await settingsRepository.GetFirstSettingsAsync(); // Fetch the first row
            if (settings == null) return TypedResults.NotFound("Settings not found.");

            // Only update the fields that are provided
            if (!string.IsNullOrEmpty(settingsDto.CommissionAccount))
                settings.CommissionAccount = settingsDto.CommissionAccount;

            settingsRepository.Update(settings);
            await settingsRepository.SaveAsync();

            return TypedResults.Ok(mapper.Map<SettingsDto>(settings)); // Return updated settings
        }
    }
}

