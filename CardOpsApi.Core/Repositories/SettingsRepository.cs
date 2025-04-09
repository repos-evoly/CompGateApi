using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using CardOpsApi.Data.Abstractions;

namespace CardOpsApi.Data.Repositories
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly CardOpsApiDbContext _context;

        public SettingsRepository(CardOpsApiDbContext context)
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
