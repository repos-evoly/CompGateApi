using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Role> Roles { get; }
        IRepository<User> Users { get; }

        IRepository<Settings> Settings { get; }

        Task SaveAsync();
    }
}
