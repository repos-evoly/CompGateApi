using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompGateApi.Data.Repositories
{
    public class ServicePackageRepository : IServicePackageRepository
    {
        private readonly CompGateApiDbContext _db;
        public ServicePackageRepository(CompGateApiDbContext db) => _db = db;

        public async Task<IList<ServicePackage>> GetAllAsync() =>
            await _db.ServicePackages.AsNoTracking().ToListAsync();

        public async Task<ServicePackage?> GetByIdAsync(int id) =>
            await _db.ServicePackages
                     .AsNoTracking()
                     .FirstOrDefaultAsync(p => p.Id == id);

        public async Task CreateAsync(ServicePackage pkg)
        {
            _db.ServicePackages.Add(pkg);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(ServicePackage pkg)
        {
            _db.ServicePackages.Update(pkg);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var pkg = await _db.ServicePackages.FindAsync(id);
            if (pkg == null) return;
            _db.ServicePackages.Remove(pkg);
            await _db.SaveChangesAsync();
        }

        public async Task<IList<ServicePackageDetail>> GetDetailsAsync(int packageId) =>
            await _db.ServicePackageDetails
                     .Where(d => d.ServicePackageId == packageId)
                     .Include(d => d.TransactionCategory)
                     .AsNoTracking()
                     .ToListAsync();
    }
}

