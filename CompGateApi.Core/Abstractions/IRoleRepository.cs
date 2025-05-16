using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IRoleRepository
    {
        Task<User> GetUserByAuthUserId(int authUserId); // Get user by AuthUserId
        Task<List<string>> GetUserPermissions(int userId);
    }
}
