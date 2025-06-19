// // ─────────────────────────────────────────────────────────────────────────────
// // CompGateApi.Data.Repositories/ServicePackageDetailRepository.cs
// // ─────────────────────────────────────────────────────────────────────────────
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using CompGateApi.Core.Abstractions;
// using CompGateApi.Data.Context;
// using CompGateApi.Data.Models;
// using Microsoft.EntityFrameworkCore;

// namespace CompGateApi.Data.Repositories
// {
//     public class ServicePackageDetailRepository : IServicePackageDetailRepository
//     {
//         private readonly CompGateApiDbContext _context;
//         public ServicePackageDetailRepository(CompGateApiDbContext context)
//             => _context = context;

//         public async Task<IList<ServicePackageDetail>> GetAllAsync(int? servicePackageId = null, int? transactionCategoryId = null)
//         {
//             var q = _context.ServicePackageDetails.AsQueryable();
//             if (servicePackageId.HasValue)
//                 q = q.Where(d => d.ServicePackageId == servicePackageId.Value);
//             if (transactionCategoryId.HasValue)
//                 q = q.Where(d => d.TransactionCategoryId == transactionCategoryId.Value);

//             return await q
//                 .AsNoTracking()
//                 .ToListAsync();
//         }

//         public async Task<ServicePackageDetail?> GetByIdAsync(int id)
//             => await _context.ServicePackageDetails
//                 .AsNoTracking()
//                 .FirstOrDefaultAsync(d => d.Id == id);

//         public async Task CreateAsync(ServicePackageDetail entity)
//         {
//             _context.ServicePackageDetails.Add(entity);
//             await _context.SaveChangesAsync();
//         }

//         public async Task UpdateAsync(ServicePackageDetail entity)
//         {
//             _context.ServicePackageDetails.Update(entity);
//             await _context.SaveChangesAsync();
//         }

//         public async Task DeleteAsync(int id)
//         {
//             var ent = await _context.ServicePackageDetails.FindAsync(id);
//             if (ent != null)
//             {
//                 _context.ServicePackageDetails.Remove(ent);
//                 await _context.SaveChangesAsync();
//             }
//         }
//     }
// }
