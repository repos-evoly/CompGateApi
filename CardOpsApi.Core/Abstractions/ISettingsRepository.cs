using CardOpsApi.Data.Models;
using System.Threading.Tasks;

namespace CardOpsApi.Data.Abstractions
{
    public interface ISettingsRepository
    {
        Task<Settings?> GetFirstSettingsAsync();
        void Update(Settings settings);
        Task SaveAsync();
    }
}
