using System.Threading.Tasks;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IAuditLogRepository
    {
        Task CreateAsync(AuditLog log);
    }
}
