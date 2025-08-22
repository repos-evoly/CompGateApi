// CompGateApi.Core.Abstractions/IVisaRepository.cs
using CompGateApi.Data.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface IVisaRepository
    {
        Task<List<Visa>> GetAllAsync(CancellationToken ct = default);
        Task<Visa?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Visa> CreateAsync(Visa entity, CancellationToken ct = default);
        Task<Visa?> UpdateAsync(int id, Visa entity, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }
}
