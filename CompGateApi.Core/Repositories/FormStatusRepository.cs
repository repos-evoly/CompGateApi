// CompGateApi.Data.Repositories/FormStatusRepository.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Repositories
{
    public class FormStatusRepository : IFormStatusRepository
    {
        private readonly CompGateApiDbContext _context;

        public FormStatusRepository(CompGateApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(FormStatus formStatus)
        {
            await _context.FormStatuses.AddAsync(formStatus);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var fs = await _context.FormStatuses.FindAsync(id);
            if (fs != null)
            {
                _context.FormStatuses.Remove(fs);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<FormStatus>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            IQueryable<FormStatus> query = _context.FormStatuses.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "nameen":
                        query = query.Where(fs => fs.NameEn.Contains(searchTerm));
                        break;
                    case "namear":
                        query = query.Where(fs => fs.NameAr.Contains(searchTerm));
                        break;
                    case "descriptionen":
                        query = query.Where(fs => fs.DescriptionEn!.Contains(searchTerm));
                        break;
                    case "descriptionar":
                        query = query.Where(fs => fs.DescriptionAr!.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(fs =>
                            fs.NameEn.Contains(searchTerm) ||
                            fs.NameAr.Contains(searchTerm) ||
                            (fs.DescriptionEn != null && fs.DescriptionEn.Contains(searchTerm)) ||
                            (fs.DescriptionAr != null && fs.DescriptionAr.Contains(searchTerm)));
                        break;
                }
            }

            return await query.OrderBy(fs => fs.Id)
                              .Skip((page - 1) * limit)
                              .Take(limit)
                              .AsNoTracking()
                              .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy)
        {
            IQueryable<FormStatus> query = _context.FormStatuses.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "nameen":
                        query = query.Where(fs => fs.NameEn.Contains(searchTerm));
                        break;
                    case "namear":
                        query = query.Where(fs => fs.NameAr.Contains(searchTerm));
                        break;
                    case "descriptionen":
                        query = query.Where(fs => fs.DescriptionEn!.Contains(searchTerm));
                        break;
                    case "descriptionar":
                        query = query.Where(fs => fs.DescriptionAr!.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(fs =>
                            fs.NameEn.Contains(searchTerm) ||
                            fs.NameAr.Contains(searchTerm) ||
                            (fs.DescriptionEn != null && fs.DescriptionEn.Contains(searchTerm)) ||
                            (fs.DescriptionAr != null && fs.DescriptionAr.Contains(searchTerm)));
                        break;
                }
            }

            return await query.CountAsync();
        }

        public async Task<FormStatus?> GetByIdAsync(int id)
        {
            return await _context.FormStatuses
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(fs => fs.Id == id);
        }

        public async Task UpdateAsync(FormStatus formStatus)
        {
            _context.FormStatuses.Update(formStatus);
            await _context.SaveChangesAsync();
        }
    }
}
