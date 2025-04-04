using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class UserActivityRepository : IUserActivityRepository
    {
        private readonly BlockingApiDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public UserActivityRepository(BlockingApiDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> UpdateUserStatus(int userId, string status)
        {
            var activity = await _context.UserActivities.FirstOrDefaultAsync(u => u.UserId == userId);
            if (activity == null)
            {
                activity = new UserActivity
                {
                    UserId = userId,
                    Status = status,
                    LastActivityTime = DateTimeOffset.Now
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                activity.Status = status;
                activity.LastActivityTime = DateTimeOffset.Now;
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserActivity?> GetUserActivity(int userId)
        {
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<List<UserActivity>> GetAllUserActivitiesForUser(int userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.BranchId })
                .FirstOrDefaultAsync();

            if (user == null)
                return new List<UserActivity>();

            int branchId = user.BranchId;

            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .Where(u => u.User.BranchId == branchId)
                .ToListAsync();
        }

        public async Task<List<UserActivity>> GetAllActivities()
        {
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .ToListAsync();
        }

        public async Task<List<UserActivity>> GetActivitiesByArea(int areaId)
        {
            return await _context.UserActivities
                .Include(u => u.User)
                .ThenInclude(user => user.Branch)
                .Where(u => u.User.Branch.AreaId == areaId)
                .ToListAsync();
        }

        public async Task<UserActivityDto?> GetUserActivityWithAuthDetailsAsync(int userId, string authToken)
        {
            var activity = await GetUserActivity(userId);
            if (activity == null)
                return null;

            var authDetails = await FetchAuthUserDetails(userId, authToken);

            return new UserActivityDto
            {
                UserId = activity.UserId,
                Status = activity.Status,
                LastActivityTime = activity.LastActivityTime,
                LastLogin = authDetails?.LastLogin,
                LastLogout = authDetails?.LastLogout
            };
        }

        // Existing 2-parameter overload (if needed elsewhere)
        public async Task<List<UserActivityDto>> GetAllUserActivitiesWithAuthDetailsAsync(int userId, string authToken)
        {
            var activities = await GetAllUserActivitiesForUser(userId);
            var distinctUserIds = activities.Select(a => a.UserId).Distinct().ToList();
            var authDetailsDict = new Dictionary<int, (DateTimeOffset? LastLogin, DateTimeOffset? LastLogout)>();

            foreach (var uid in distinctUserIds)
            {
                var authDetails = await FetchAuthUserDetails(uid, authToken);
                if (authDetails != null)
                    authDetailsDict[uid] = (authDetails.LastLogin, authDetails.LastLogout);
            }

            return activities.Select(a => new UserActivityDto
            {
                UserId = a.UserId,
                Status = a.Status,
                LastActivityTime = a.LastActivityTime,
                LastLogin = authDetailsDict.ContainsKey(a.UserId) ? authDetailsDict[a.UserId].LastLogin : null,
                LastLogout = authDetailsDict.ContainsKey(a.UserId) ? authDetailsDict[a.UserId].LastLogout : null
            }).ToList();
        }

        // New overload with filtering by branch and area.
        public async Task<List<UserActivityDto>> GetAllUserActivitiesWithAuthDetailsAsync(int userId, string authToken, string? branchFilter, int? areaFilter)
        {
            List<UserActivity> activities;

            // If any filter is provided, start with all activities.
            if (!string.IsNullOrEmpty(branchFilter) || areaFilter.HasValue)
            {
                activities = await GetAllActivities();
                if (!string.IsNullOrEmpty(branchFilter))
                {
                    activities = activities.Where(a => a.User.Branch?.CABBN == branchFilter).ToList();
                }
                if (areaFilter.HasValue)
                {
                    activities = activities.Where(a => a.User.Branch?.AreaId == areaFilter.Value).ToList();
                }
            }
            else
            {
                // If no filter is provided:
                // - For maker, we force his own area (handled in endpoint)
                // - For manager-like roles, we want all activities.
                activities = await GetAllActivities();
            }

            var distinctUserIds = activities.Select(a => a.UserId).Distinct().ToList();
            var authDetailsDict = new Dictionary<int, (DateTimeOffset? LastLogin, DateTimeOffset? LastLogout)>();

            foreach (var uid in distinctUserIds)
            {
                var authDetails = await FetchAuthUserDetails(uid, authToken);
                if (authDetails != null)
                    authDetailsDict[uid] = (authDetails.LastLogin, authDetails.LastLogout);
            }

            return activities.Select(a => new UserActivityDto
            {
                UserId = a.UserId,
                Status = a.Status,
                LastActivityTime = a.LastActivityTime,
                LastLogin = authDetailsDict.ContainsKey(a.UserId) ? authDetailsDict[a.UserId].LastLogin : null,
                LastLogout = authDetailsDict.ContainsKey(a.UserId) ? authDetailsDict[a.UserId].LastLogout : null
            }).ToList();
        }

        private async Task<AuthUserDto?> FetchAuthUserDetails(int userId, string authToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AuthClient");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                var response = await client.GetAsync($"http://10.3.3.11/authapi/api/users/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Auth JSON: {json}");
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<AuthUserDto>(json, options);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching auth details: {ex.Message}");
            }
            return null;
        }

    }
}
