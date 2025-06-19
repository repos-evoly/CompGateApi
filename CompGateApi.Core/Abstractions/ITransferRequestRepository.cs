using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CompGateApi.Core.Abstractions
{
    public interface ITransferRequestRepository
    {
        // company‐scoped (for “my transfers”)
        Task<int> GetCountByCompanyAsync(int companyId, string? searchTerm);
        Task<List<TransferRequest>> GetAllByCompanyAsync(int companyId, string? searchTerm, int page, int limit);

        // admin‐scoped
        Task<int> GetCountAsync(string? searchTerm);
        Task<List<TransferRequest>> GetAllAsync(string? searchTerm, int page, int limit);

        // single fetch
        Task<TransferRequest?> GetByIdAsync(int id);

        // create & update
        Task CreateAsync(TransferRequest tr);
        Task UpdateAsync(TransferRequest tr);

        // external lookups
        Task<List<AccountDto>> GetAccountsAsync(string codeOrAccount);

        Task<string?> GetStCodeByAccount(string account);
        Task<List<StatementEntryDto>> GetStatementAsync(string account, DateTime from, DateTime to);
    }
}
