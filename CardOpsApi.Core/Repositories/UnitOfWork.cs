using CardOpsApi.Core.Abstractions;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using System;
using System.Threading.Tasks;

namespace CardOpsApi.Core.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CardOpsApiDbContext _context;

        public IRepository<Role> Roles { get; }
        public IRepository<User> Users { get; }
        public IRepository<Settings> Settings { get; }

        public UnitOfWork(
            CardOpsApiDbContext context,
            IRepository<Role> rolesRepo,
            IRepository<User> usersRepo,
            IRepository<Settings> settingsRepo)
        {
            _context = context;
            Roles = rolesRepo;
            Users = usersRepo;
            Settings = settingsRepo;
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
