using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BlockingApi.Core.Dtos;
using BlockingApi.Core.Abstractions;
using BlockingApi.Data.Abstractions;
using BlockingApi.Abstractions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace BlockingApi.Endpoints
{
    public class UserActivityEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var activity = app.MapGroup("/api/user-activity").RequireAuthorization("requireAuthUser");

            activity.MapPost("/update", UpdateUserActivity)
                .Produces(200)
                .Produces(400);

            activity.MapGet("/user", GetUserActivities)
                .Produces<UserActivityDto>(200)
                .Produces(404);

            activity.MapGet("/all", GetAllUserActivities)
                .Produces<List<UserActivityDto>>(200);
        }

        // POST: Update the user's activity status.
        public static async Task<IResult> UpdateUserActivity(
            [FromBody] UserActivityDto activityDto,
            [FromServices] IUserActivityRepository userActivityRepository,
            ILogger<UserActivityEndpoints> logger)
        {
            logger.LogInformation("Updating user activity: UserId {UserId} - Status {Status}", activityDto.UserId, activityDto.Status);
            bool updated = await userActivityRepository.UpdateUserStatus(activityDto.UserId, activityDto.Status);
            return updated ? Results.Ok("User activity updated.") : Results.BadRequest("Failed to update activity.");
        }

        // GET: Return the current user's own activity enriched with auth details.
        public static async Task<IResult> GetUserActivities(
            HttpContext context,
            [FromServices] IUserActivityRepository userActivityRepository,
            [FromServices] IUserRepository userRepository)
        {
            int currentUserId = AuthUserId(context);
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var currentUserDetails = await userRepository.GetUserByAuthId(currentUserId, authToken);
            if (currentUserDetails == null)
                return Results.BadRequest("Invalid user.");

            var dto = await userActivityRepository.GetUserActivityWithAuthDetailsAsync(currentUserId, authToken);
            if (dto == null)
                return Results.NotFound("User activity not found.");
            return Results.Ok(dto);
        }

        // GET: Return all user activities for the current user's branch.
        // Makers see only activities in their area (forced area filter) and can optionally filter by branch.
        // Managers, AssistantManagers, and DeputyManagers see all activities and can filter by branch or area.
        public static async Task<IResult> GetAllUserActivities(
    [FromQuery] string? branch,
    [FromQuery] int? area,
    HttpContext context,
    [FromServices] IUserActivityRepository userActivityRepository,
    [FromServices] IUserRepository userRepository)
        {
            if (!context.User.Identity?.IsAuthenticated ?? false)
                return Results.Unauthorized();

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
                return Results.Unauthorized();

            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var currentUserDetails = await userRepository.GetUserByAuthId(currentUserId, authToken);
            if (currentUserDetails == null)
                return Results.BadRequest("Invalid user.");

            // Convert role to lowercase for consistent comparison.
            string role = currentUserDetails.Role?.NameLT.ToLower() ?? "";

            List<UserActivityDto> dtos = null;
            if (role == "maker")
            {
                // For Maker, force area filter to his own area.
                if (area.HasValue && area.Value != currentUserDetails.AreaId)
                {
                    return Results.BadRequest("As a Maker, you can only view your own area.");
                }
                dtos = await userActivityRepository.GetAllUserActivitiesWithAuthDetailsAsync(currentUserId, authToken, branch, currentUserDetails.AreaId);
            }
            else if (role == "manager" || role == "assistantmanager" || role == "deputymanager" ||
                     role == "admin" || role == "superadmin")
            {
                // For these roles, if no filter is provided, show all activities.
                // Otherwise, use the provided branch and/or area filter.
                if (string.IsNullOrEmpty(branch) && !area.HasValue)
                {
                    dtos = await userActivityRepository.GetAllUserActivitiesWithAuthDetailsAsync(currentUserId, authToken, null, null);
                }
                else
                {
                    dtos = await userActivityRepository.GetAllUserActivitiesWithAuthDetailsAsync(currentUserId, authToken, branch, area);
                }
            }
            else
            {
                // For all other roles, only return the current user's activity.
                var single = await userActivityRepository.GetUserActivityWithAuthDetailsAsync(currentUserId, authToken);
                if (single == null)
                    return Results.NotFound("User activity not found.");
                return Results.Ok(single);
            }

            return Results.Ok(dtos);
        }




        // Helper: Extract the authenticated user's ID from claims.
        private static int AuthUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }
    }
}
