using BlockingApi.Core.Abstractions;
using BlockingApi.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using BlockingApi.Core.Dtos;
using BlockingApi.Abstractions;
using System.Security.Claims;

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

            activity.MapGet("/{userId:int}", GetUserActivity)
                .Produces<UserActivityDto>(200)
                .Produces(404);

            activity.MapGet("/all", GetAllUserActivities)
                .Produces<List<UserActivityDto>>(200);
        }

        public static async Task<IResult> UpdateUserActivity(
    [FromBody] UserActivityDto activityDto,
    [FromServices] IUserActivityRepository userActivityRepository,
    ILogger<UserActivityEndpoints> logger)
        {
            logger.LogInformation("Updating user activity: UserId {UserId} - Status {Status}", activityDto.UserId, activityDto.Status);

            bool updated = await userActivityRepository.UpdateUserStatus(activityDto.UserId, activityDto.Status);
            return updated ? Results.Ok("User activity updated.") : Results.BadRequest("Failed to update activity.");
        }


        public static async Task<IResult> GetUserActivity(
    [FromRoute] int userId,
    [FromServices] IUserActivityRepository userActivityRepository)
        {
            var activity = await userActivityRepository.GetUserActivity(userId);
            if (activity == null)
                return Results.NotFound("User activity not found.");

            return Results.Ok(new
            {
                activity.UserId,
                activity.Status,
                activity.LastActivityTime,

                User = new
                {
                    activity.User.FirstName,
                    activity.User.LastName,
                    activity.User.Email
                },

                Branch = new
                {
                    activity.User.Branch.Name
                }
            });
        }



        public static async Task<IResult> GetAllUserActivities(
     [FromServices] IUserActivityRepository userActivityRepository,
     HttpContext httpContext)
        {
            // 🔥 Check if the user is authenticated
            if (!httpContext.User.Identity?.IsAuthenticated ?? false)
            {
                Console.WriteLine("❌ User is NOT authenticated!");
                return Results.Unauthorized();
            }
            Console.WriteLine("✅ User IS authenticated!");

            // 🔍 Log all claims to verify correct claim extraction
            foreach (var claim in httpContext.User.Claims)
            {
                Console.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
            }

            // ✅ Extract UserId from multiple possible claims
            var userIdClaim = httpContext.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value // 🔥 Standard .NET claim type
                ?? httpContext.User.Claims.FirstOrDefault(c => c.Type == "nameidentifier")?.Value // 🔥 Raw JWT claim
                ?? httpContext.User.Claims.FirstOrDefault(c => c.Type == "sid")?.Value; // 🔥 Alternate claim

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                Console.WriteLine("❌ User ID is missing or invalid in token!");
                return Results.Unauthorized();
            }

            Console.WriteLine($"✅ Extracted UserId: {userId}");

            // ✅ Fetch user activities for this user’s branch
            var activities = await userActivityRepository.GetAllUserActivitiesForUser(userId);

            return Results.Ok(activities.Select(activity => new
            {
                UserId = activity.UserId,
                Status = activity.Status,
                LastActivityTime = activity.LastActivityTime,

                User = new
                {
                    activity.User.FirstName,
                    activity.User.LastName,
                    activity.User.Email
                },

                Branch = new
                {
                    activity.User.Branch.Name
                }
            }));
        }

    }
}