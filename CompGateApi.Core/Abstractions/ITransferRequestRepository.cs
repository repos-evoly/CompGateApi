// CompGateApi.Core.Abstractions/ITransferRequestRepository.cs
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ITransferRequestRepository
    {
        // user‐scoped
        Task<List<TransferRequest>> GetAllByUserAsync(int userId, string? searchTerm, int page, int limit);
        Task<int> GetCountByUserAsync(int userId, string? searchTerm);
        Task<TransferRequest?> GetByIdAsync(int id);
        Task CreateAsync(TransferRequest entity);

        // external‐API calls
        Task<List<AccountDto>> GetAccountsAsync(string codeOrAccount);
        Task<List<StatementEntryDto>> GetStatementAsync(string account, DateTime from, DateTime to);

        // admin
        Task<List<TransferRequest>> GetAllAsync(string? searchTerm, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm);
        Task UpdateAsync(TransferRequest entity);
    }
}
