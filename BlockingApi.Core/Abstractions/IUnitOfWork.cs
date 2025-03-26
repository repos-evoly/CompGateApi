using BlockingApi.Data.Models;

namespace BlockingApi.Core.Abstractions
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Role> Roles { get; }
        IRepository<User> Users { get; }
        IRepository<Reason> Reasons { get; }
        IRepository<Source> Sources { get; }
        IRepository<Settings> Settings { get; }

        Task SaveAsync();
    }
}
