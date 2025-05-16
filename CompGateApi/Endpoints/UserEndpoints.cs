using CompGateApi.Data.Models;
using CompGateApi.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CompGateApi.Abstractions;
using CompGateApi.Core.Dtos;
using System.Text.Json;
using AutoMapper;
using System.Net.Http.Headers;

namespace CompGateApi.Endpoints
{
    public class UserEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {

            // app.MapPost("/api/users/register", RegisterCompany)
            //    .WithName("RegisterCompany")
            //    .Accepts<CompanyRegistrationDto>("application/json")
            //    .Produces(200)
            //    .Produces(400);

            var users = app.MapGroup("/api/users").RequireAuthorization("RequireCompanyUser");

            users.MapPost("/add", AddUser)
                 .WithName("AddUser")
                 .Accepts<UserRegistrationDto>("application/json")
                 .Produces(200)
                 .Produces(400);

            users.MapPost("/assign-role", AssignRole)

                .Produces(200)
                .Produces(400);

            users.MapPost("/assign-permissions", AssignUserPermissions)

                .Produces(200)
                .Produces(400);

            users.MapGet("/", GetUsers)

                .Produces(200);

            users.MapGet("/{userId:int}", GetUserById)

                .Produces(200);

            users.MapPut("/edit/{userId:int}", EditUser)

                .Produces(200)
                .Produces(400);

            users.MapPut("/edit-permissions/{userId:int}", EditUserRolePermissions)

                .Produces(200)
                .Produces(400);

            users.MapGet("/roles", GetRoles)

                .Produces<List<Role>>(200);

            users.MapGet("/permissions", GetPermissions)

                .Produces<List<Permission>>(200);

            users.MapGet("/{userId:int}/permissions", GetUserPermissions)

                .Produces<List<string>>(200)
                .Produces(404);

            users.MapGet("/by-auth/{authId:int}", GetUserByAuthId)

                .Produces(200)
                .Produces(404);

            users.MapGet("/management", GetManagementUsers)

                .Produces<List<UserDetailsDto>>(200);

            users.MapPost("cookies/set", SetCookies)

            .Produces(200);

        }

        public class CookieDto
        {
            public Dictionary<string, string> Cookies { get; set; } = new Dictionary<string, string>();
        }


        public static async Task<IResult> SetCookies([FromBody] CookieDto cookieDto, HttpContext context)
        {
            // Define cookie options (customize as needed).
            var options = new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddDays(15),
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };

            // Iterate over the provided cookies and set each one.
            foreach (var kvp in cookieDto.Cookies)
            {
                context.Response.Cookies.Append(kvp.Key, kvp.Value, options);
            }

            return Results.Ok("Cookies set successfully.");
        }

        public static async Task<IResult> RegisterCompany(
            [FromBody] CompanyRegistrationDto dto,
            [FromServices] IUserRepository repo,
            ILogger<UserEndpoints> log)
        {
            // 1️⃣ Call Auth register
            var authPayload = new
            {
                username = dto.Username,
                fullNameLT = dto.FirstName,
                fullNameAR = dto.LastName,
                email = dto.Email,
                password = dto.Password,
                roleId = dto.RoleId
            };

            using var http = new HttpClient();
            var resp = await http.PostAsJsonAsync(
                "http://10.3.3.11/compauthapi/api/auth/register",
                authPayload);

            if (!resp.IsSuccessStatusCode)
                return Results.BadRequest("Auth registration failed.");

            var body = await resp.Content.ReadAsStringAsync();
            var reg = JsonSerializer.Deserialize<AuthRegisterResponseDto>(body,
                          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (reg.userId == 0)
                return Results.BadRequest("Auth returned invalid userId.");

            // 2️⃣ Persist local company user
            var user = new User
            {
                AuthUserId = reg.userId,
                CompanyId = dto.CompanyId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                RoleId = dto.RoleId
            };

            if (!await repo.AddUser(user))
                return Results.BadRequest("Failed to save company locally.");

            return Results.Ok(new
            {
                reg.message,
                LocalCompanyId = user.Id,
                AuthUserId = reg.userId
            });
        }

        /// <summary>
        /// Protected endpoint for admin panel to add back-office users.
        /// </summary>
        [Authorize]  // Optional if you want method-level; group-level is enough.
        public static async Task<IResult> AddUser(
     [FromBody] UserRegistrationDto dto,
     [FromServices] IUserRepository repo,
     [FromServices] IHttpClientFactory httpFactory,
     [FromServices] ILogger<UserEndpoints> logger,
     [FromServices] IHttpContextAccessor accessor
 )
        {
            // 1) Extract auth token
            var token = accessor.HttpContext?
                             .Request
                             .Headers["Authorization"]
                             .ToString()
                             .Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
            {
                logger.LogError("Missing auth token.");
                return Results.BadRequest("Missing authentication token.");
            }

            // 2) Build Auth payload
            var authPayload = new
            {
                username = dto.Username,
                fullNameLT = dto.FirstName,
                fullNameAR = dto.LastName,
                email = dto.Email,
                password = dto.Password,
                roleId = dto.RoleId
            };

            // 3) Call Auth API
            var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PostAsJsonAsync(
                "http://10.3.3.11/compauthapi/api/auth/register",
                authPayload);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("Auth registration failed: {Status}", resp.StatusCode);
                return Results.BadRequest("Failed to register user in Auth system.");
            }

            var body = await resp.Content.ReadAsStringAsync();
            var reg = JsonSerializer.Deserialize<AuthRegisterResponseDto>(body,
                          new JsonSerializerOptions
                          { PropertyNameCaseInsensitive = true })!;
            if (reg.userId == 0)
                return Results.BadRequest("Auth returned invalid userId.");

            // 4) Persist locally
            var user = new User
            {
                AuthUserId = reg.userId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                RoleId = dto.RoleId
            };

            if (!await repo.AddUser(user))
            {
                return Results.BadRequest("Failed to save user locally.");
            }

            return Results.Ok(new
            {
                Message = reg.message,
                LocalUserId = user.Id,
                AuthUserId = reg.userId
            });
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
           [FromServices] IMapper mapper,
           [FromQuery] string? searchTerm,
           [FromQuery] string? searchBy,
           [FromQuery] int page = 1,
           [FromQuery] int limit = 100000)
        {
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            // Retrieve paginated users.
            var users = await userRepository.GetUsersAsync(searchTerm, searchBy, page, limit, authToken);

            // Retrieve total count for the filters.
            int totalCount = await userRepository.GetUserCountAsync(searchTerm, searchBy);
            int totalPages = (int)System.Math.Ceiling((double)totalCount / limit);

            return Results.Ok(new { Data = users, TotalPages = totalPages });
        }

        public static async Task<IResult> GetUserById(
            int id,
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            var authToken = context.Request.Headers["Authorization"]
                                   .ToString()
                                   .Replace("Bearer ", "");
            if (string.IsNullOrEmpty(authToken))
                return Results.BadRequest("Missing authentication token.");

            var user = await userRepository.GetUserById(id, authToken);
            return user is null
                ? Results.NotFound("User not found.")
                : Results.Ok(user);
        }


        public static async Task<IResult> GetManagementUsers(
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            ILogger<UserEndpoints> logger)
        {
            int currentUserId = AuthUserId(context);
            var authToken = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var currentUser = await userRepository.GetUserByAuthId(currentUserId, authToken);
            if (currentUser == null)
                return Results.BadRequest("Invalid user.");

            // Use the role from the fetched user details.
            string currentUserRole = currentUser.Role.NameLT;
            var managementUsers = await userRepository.GetManagementUsersAsync(currentUserRole);
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

        private static int AuthUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

    }
}
