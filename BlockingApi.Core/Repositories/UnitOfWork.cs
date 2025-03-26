using BlockingApi.Core.Abstractions;
using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using System;
using System.Threading.Tasks;

namespace BlockingApi.Core.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BlockingApiDbContext _context;

        public IRepository<Role> Roles { get; }
        public IRepository<User> Users { get; }
        public IRepository<Reason> Reasons { get; }
        public IRepository<Source> Sources { get; }
        public IRepository<Settings> Settings { get; }

        public UnitOfWork(
            BlockingApiDbContext context,
            IRepository<Role> rolesRepo,
            IRepository<User> usersRepo,
            IRepository<Reason> reasonsRepo,
            IRepository<Source> sourcesRepo,
            IRepository<Settings> settingsRepo)
        {
            _context = context;
            Roles = rolesRepo;
            Users = usersRepo;
            Reasons = reasonsRepo;
            Sources = sourcesRepo;
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
