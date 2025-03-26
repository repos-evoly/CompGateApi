using BlockingApi.Data.Models;
using System.Threading.Tasks;

namespace BlockingApi.Data.Abstractions
{
    public interface ISettingsRepository
    {
        Task<Settings?> GetFirstSettingsAsync();
        void Update(Settings settings);
        Task SaveAsync();
    }
}
