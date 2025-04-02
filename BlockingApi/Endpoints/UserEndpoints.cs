using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BlockingApi.Abstractions;
using BlockingApi.Core.Dtos;
using System.Text.Json;

namespace BlockingApi.Endpoints
{
    public class UserEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var users = app.MapGroup("/api/users").RequireAuthorization("requireAuthUser");

            users.MapPost("/add", AddUser)
                .RequireAuthorization("ManageUsers")
                .Produces(200)
                .Produces(400);

            users.MapPost("/assign-role", AssignRole)
                .RequireAuthorization("ManageUsers")
                .Produces(200)
                .Produces(400);

            users.MapPost("/assign-permissions", AssignUserPermissions)
                .RequireAuthorization("ManageUsers")
                .Produces(200)
                .Produces(400);

            users.MapGet("/", GetUsers)
                .RequireAuthorization("ManageUsers")
                .Produces(200);

            users.MapGet("/{userId:int}", GetUserById)
                .RequireAuthorization("ManageUsers")
                .Produces(200);

            users.MapPut("/edit/{userId:int}", EditUser)
                .RequireAuthorization("ManageUsers")
                .Produces(200)
                .Produces(400);

            users.MapPut("/edit-permissions/{userId:int}", EditUserRolePermissions)
                .RequireAuthorization("ManageUsers")
                .Produces(200)
                .Produces(400);

            users.MapGet("/roles", GetRoles)
                .RequireAuthorization("ManageUsers")
                .Produces<List<Role>>(200);

            users.MapGet("/permissions", GetPermissions)
                .RequireAuthorization("ManageUsers")
                .Produces<List<Permission>>(200);

            users.MapGet("/{userId:int}/permissions", GetUserPermissions)
                .RequireAuthorization("ManageUsers")
                .Produces<List<string>>(200)
                .Produces(404);

            users.MapGet("/by-auth/{authId:int}", GetUserByAuthId)
                .RequireAuthorization("ManageUsers")
                .Produces(200)
                .Produces(404);

            users.MapGet("/management", GetManagementUsers)
                .RequireAuthorization("ManageUsers")
                .Produces<List<UserDetailsDto>>(200);

        }

        public static async Task<IResult> AddUser(
     [FromBody] UserRegistrationDto userDto,
     [FromServices] IUserRepository userRepository,
     HttpContext context,
     ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Registering new user in Auth system for Email: {Email}", userDto.Email);

            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(authToken))
            {
                logger.LogError("Missing JWT token.");
                return Results.BadRequest("Missing authentication token.");
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var authPayload = new
            {
                fullNameLT = userDto.FirstName,
                fullNameAR = userDto.FirstName,
                email = userDto.Email,
                password = userDto.Password,
                roleId = userDto.RoleId
            };

            var jsonPayload = JsonSerializer.Serialize(authPayload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("http://10.3.3.11/authapi/api/auth/register", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // ✅ Check if response body is empty or malformed
            if (string.IsNullOrEmpty(responseBody))
            {
                logger.LogError("Auth response body is empty.");
                return Results.BadRequest("Failed to get a valid response from authentication system.");
            }

            AuthRegisterResponseDto? authResponse = null;

            try
            {
                authResponse = JsonSerializer.Deserialize<AuthRegisterResponseDto>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Auth response.");
                return Results.BadRequest("Invalid response from authentication system.");
            }

            if (!response.IsSuccessStatusCode || authResponse == null || authResponse.userId == 0)
            {
                logger.LogError("Auth registration failed. Message: {Message}", authResponse?.message ?? "No message");
                return Results.BadRequest(authResponse?.message ?? "Failed to register user in authentication system.");
            }

            // 🔹 Proceed to Blocking DB
            var blockingUser = new User
            {
                AuthUserId = authResponse.userId,
                FirstName = userDto.FirstName,
                LastName = userDto.FirstName, // Optional split
                Email = userDto.Email,
                Phone = userDto.Phone,
                RoleId = userDto.RoleId,
                BranchId = userDto.BranchId
            };

            var result = await userRepository.AddUser(blockingUser);

            return result ? Results.Ok("User registered successfully.") : Results.BadRequest("Failed to add user to blocking system.");
        }




        public static async Task<IResult> AssignRole(
            [FromBody] AssignRoleDto assignRoleDto,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Assigning RoleId {RoleId} to UserId {UserId}", assignRoleDto.RoleId, assignRoleDto.UserId);

            var result = await userRepository.AssignRole(assignRoleDto.UserId, assignRoleDto.RoleId);
            return result ? Results.Ok("Role assigned successfully.") : Results.BadRequest("Failed to assign role.");
        }

        public static async Task<IResult> AssignUserPermissions(
     [FromBody] AssignUserPermissionsDto assignUserPermissionsDto,
     [FromServices] IUserRepository userRepository,
     ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Assigning Permissions to UserId {UserId}", assignUserPermissionsDto.UserId);

            var result = await userRepository.AssignUserPermissions(assignUserPermissionsDto.UserId, assignUserPermissionsDto.Permissions);
            return result ? Results.Ok("Permissions assigned successfully.") : Results.BadRequest("Failed to assign permissions.");
        }


        public static async Task<IResult> GetUsers(
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            logger.LogInformation("Fetching all users with authentication details");
            var users = await userRepository.GetUsers(authToken);
            return Results.Ok(users);
        }

        public static async Task<IResult> GetUserById(
            int userId,
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            logger.LogInformation("Fetching user with ID {UserId}", userId);
            var user = await userRepository.GetUserById(userId, authToken);
            return user != null ? Results.Ok(user) : Results.NotFound("User not found.");
        }

        public static async Task<IResult> GetManagementUsers(
             [FromServices] IUserRepository userRepository,
             ILogger<UserEndpoints> logger)
        {
            var managementUsers = await userRepository.GetManagementUsersAsync();
            logger.LogInformation("Retrieved {Count} management users.", managementUsers.Count);
            return Results.Ok(managementUsers);
        }


        public static async Task<IResult> GetUserByAuthId(
          int authId,
          HttpContext context,
          [FromServices] IUserRepository userRepository,
          ILogger<UserEndpoints> logger)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            logger.LogInformation("Fetching user with AuthUserId {AuthId}", authId);
            var user = await userRepository.GetUserByAuthId(authId, authToken);
            return user != null ? Results.Ok(user) : Results.NotFound("User not found.");
        }



        public static async Task<IResult> EditUser(
            int userId,
            [FromBody] EditUserDto editUserDto,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Editing user with ID {UserId}", userId);

            var result = await userRepository.EditUser(userId, editUserDto);
            return result ? Results.Ok("User updated successfully.") : Results.BadRequest("Failed to update user.");
        }

        public static async Task<IResult> EditUserRolePermissions(
            int userId,
            [FromBody] AssignUserPermissionsDto assignUserPermissionsDto,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Editing permissions for UserId {UserId}", userId);

            var result = await userRepository.AssignUserPermissions(userId, assignUserPermissionsDto.Permissions);
            return result ? Results.Ok("User permissions updated successfully.") : Results.BadRequest("Failed to update user permissions.");
        }



        public static async Task<IResult> GetUserPermissions(
            int userId,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Fetching permissions for UserId {UserId}", userId);

            // Call the repository method to fetch permissions with status
            var permissions = await userRepository.GetUserPermissions(userId);

            if (permissions == null || !permissions.Any())
            {
                return Results.NotFound("No permissions found for this user.");
            }

            return Results.Ok(permissions); // Return permissions with 0/1 value
        }



        public static async Task<IResult> GetRoles(
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Fetching all roles");
            var roles = await userRepository.GetRoles();
            return Results.Ok(roles);
        }

        public static async Task<IResult> GetPermissions(
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            logger.LogInformation("Fetching all permissions");
            var permissions = await userRepository.GetPermissions();
            return Results.Ok(permissions);
        }

    }
}
