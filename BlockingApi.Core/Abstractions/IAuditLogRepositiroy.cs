using BlockingApi.Data.Models;
using System.Threading.Tasks;

namespace BlockingApi.Data.Abstractions
{

    public interface IAuditLogRepository
    {
   
        Task AddAuditLog(AuditLog log);
    }
}
