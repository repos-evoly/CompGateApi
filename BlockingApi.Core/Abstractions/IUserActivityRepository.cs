using System.Collections.Generic;
using System.Threading.Tasks;
using BlockingApi.Data.Models;

namespace BlockingApi.Core.Abstractions
{
    public interface IUserActivityRepository
    {
        Task<bool> UpdateUserStatus(int userId, string status);
        Task<UserActivity?> GetUserActivity(int userId);
        Task<List<UserActivity>> GetAllUserActivitiesForUser(int userId);

        // New methods for global retrieval and filtering by area.
        Task<List<UserActivity>> GetAllActivities();
        Task<List<UserActivity>> GetActivitiesByArea(int areaId);
    }
}
