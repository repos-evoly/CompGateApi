using System.Collections.Generic;
using System.Threading.Tasks;
using BlockingApi.Data.Models;

namespace BlockingApi.Core.Abstractions
{
    public interface IRoleRepository
    {
        Task<User> GetUserByAuthUserId(int authUserId); // Get user by AuthUserId
        Task<List<string>> GetUserPermissions(int userId);
    }
}
