using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly CompGateApiDbContext _context;

        public AuditLogRepository(CompGateApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(AuditLog log)
        {
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
