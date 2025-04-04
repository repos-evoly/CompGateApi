using System.Collections.Generic;
using System.Threading.Tasks;
using BlockingApi.Data.Models;
using BlockingApi.Core.Dtos;

namespace BlockingApi.Core.Abstractions
{
    public interface IUserActivityRepository
    {
        Task<bool> UpdateUserStatus(int userId, string status);
        Task<UserActivity?> GetUserActivity(int userId);
        Task<List<UserActivity>> GetAllUserActivitiesForUser(int userId);
        Task<List<UserActivity>> GetAllActivities();
        Task<List<UserActivity>> GetActivitiesByArea(int areaId);

        // Existing method (2 parameters)
        Task<List<UserActivityDto>> GetAllUserActivitiesWithAuthDetailsAsync(int userId, string authToken);

        // New overload (4 parameters) that supports filtering
        Task<List<UserActivityDto>> GetAllUserActivitiesWithAuthDetailsAsync(int userId, string authToken, string? branchFilter, int? areaFilter);

        Task<UserActivityDto?> GetUserActivityWithAuthDetailsAsync(int userId, string authToken);
    }
}
