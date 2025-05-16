using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using CompGateApi.Data.Abstractions;

namespace CompGateApi.Data.Repositories
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly CompGateApiDbContext _context;

        public SettingsRepository(CompGateApiDbContext context)
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
