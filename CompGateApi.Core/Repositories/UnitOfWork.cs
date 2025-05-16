using CompGateApi.Core.Abstractions;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using System;
using System.Threading.Tasks;

namespace CompGateApi.Core.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CompGateApiDbContext _context;

        public IRepository<Role> Roles { get; }
        public IRepository<User> Users { get; }
        public IRepository<Settings> Settings { get; }

        public UnitOfWork(
            CompGateApiDbContext context,
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
