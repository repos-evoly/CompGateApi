using CompGateApi.Data.Models;
using System.Threading.Tasks;

namespace CompGateApi.Data.Abstractions
{
    public interface ISettingsRepository
    {
        Task<Settings?> GetFirstSettingsAsync();
        void Update(Settings settings);
        Task SaveAsync();
    }
}
