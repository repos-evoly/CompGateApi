using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly CompGateApiDbContext _db;

        public AuditLogRepository(CompGateApiDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(AuditLog log)
        {
            _db.Set<AuditLog>().Add(log);
            await _db.SaveChangesAsync();
        }
    }
}
