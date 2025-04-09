using System.Collections.Generic;
using System.Threading.Tasks;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IRoleRepository
    {
        Task<User> GetUserByAuthUserId(int authUserId); // Get user by AuthUserId
        Task<List<string>> GetUserPermissions(int userId);
    }
}
