using System.Threading.Tasks;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly CardOpsApiDbContext _context;

        public AuditLogRepository(CardOpsApiDbContext context)
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
