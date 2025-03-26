using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using BlockingApi.Data.Abstractions;

namespace BlockingApi.Data.Repositories
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly BlockingApiDbContext _context;

        public SettingsRepository(BlockingApiDbContext context)
        {
            _context = context;
        }

        // 🔹 Get the first settings row in the table
        public async Task<Settings?> GetFirstSettingsAsync()
        {
            return await _context.Settings.FirstOrDefaultAsync();
        }

        // 🔹 Update settings
        public void Update(Settings settings)
        {
            _context.Settings.Update(settings);
        }

        // 🔹 Save changes to the database
        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
