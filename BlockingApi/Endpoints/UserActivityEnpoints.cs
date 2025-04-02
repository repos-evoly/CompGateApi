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

            activity.MapGet("/user", GetUserActivities)
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


        public static async Task<IResult> GetUserActivities(
       HttpContext context,
       [FromServices] IUserRepository userRepository,
       [FromServices] IUserActivityRepository userActivityRepository,
       // Optional filters
       [FromQuery] string? branchId,
       [FromQuery] int? areaId,
       ILogger<UserActivityEndpoints> logger)
        {
            // Retrieve the current user from the token
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var currentUserAuthId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var currentUser = await userRepository.GetUserByAuthId(currentUserAuthId, authToken);
            if (currentUser == null)
                return Results.BadRequest("Invalid user.");

            // Determine role (assuming Role.Name is available)
            var roleName = currentUser.Role.ToLower();

            List<UserActivity> activities = new List<UserActivity>();

            if (roleName.Contains("manager") || roleName.Contains("deputymanager") || roleName.Contains("assistantmanager"))
            {
                // For Manager-like roles, retrieve all activities.
                // (Assumes you have implemented GetAllActivities in your repository)
                activities = await userActivityRepository.GetAllActivities();

                // Apply branch or area filters if provided
                if (branchId != null)
                {
                    activities = activities.Where(a => a.User.Branch.CABBN == branchId).ToList();
                }
                else if (areaId.HasValue)
                {
                    activities = activities.Where(a => a.User.Branch.AreaId == areaId.Value).ToList();
                }
            }
            else if (roleName.Contains("maker"))
            {
                // For Maker, only allow activities from his own area.
                int makerAreaId = currentUser.Branch.AreaId;
                // (Assumes you have implemented GetActivitiesByArea in your repository)
                activities = await userActivityRepository.GetActivitiesByArea(makerAreaId);

                // If a branch filter is provided, further filter the result.
                if (branchId != null)
                {
                    activities = activities.Where(a => a.User.Branch.CABBN == branchId).ToList();
                }
            }
            else
            {
                // Fallback: only return the current user's activity.
                var activity = await userActivityRepository.GetUserActivity(currentUser.UserId);
                if (activity != null)
                    activities.Add(activity);
            }

            return Results.Ok(activities);
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