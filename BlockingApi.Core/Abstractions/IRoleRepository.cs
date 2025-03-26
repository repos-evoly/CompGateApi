using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlockingApi.Core.Abstractions
{
    public interface IRoleRepository
    {
        Task<List<string>> GetUserPermissions(int userId);
    }
}
