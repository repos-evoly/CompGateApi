using BlockingApi.Data.Abstractions;
using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using System.Threading.Tasks;

namespace BlockingApi.Data.Repositories
{
    /// <summary>
    /// Concrete implementation for inserting audit logs into the DB.
    /// </summary>
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly BlockingApiDbContext _context;

        public AuditLogRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        public async Task AddAuditLog(AuditLog log)
        {
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
