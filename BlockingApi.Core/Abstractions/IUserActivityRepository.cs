using BlockingApi.Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlockingApi.Core.Abstractions
{
    public interface IUserActivityRepository
    {

        Task<bool> UpdateUserStatus(int userId, string status);
        Task<UserActivity?> GetUserActivity(int userId);
        Task<List<UserActivity>> GetAllUserActivitiesForUser(int userId);
    }
}
