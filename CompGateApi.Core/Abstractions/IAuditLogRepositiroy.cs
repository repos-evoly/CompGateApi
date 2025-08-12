using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog log);
    }
}
