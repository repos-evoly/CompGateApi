using System.Collections.Generic;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    public interface ICreditFacilitiesOrLetterOfGuaranteeRequestRepository
    {
        // COMPANY: only this user’s requests
        Task<IList<CreditFacilitiesOrLetterOfGuaranteeRequest>> GetAllByUserAsync(
            int userId, string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountByUserAsync(
            int userId, string? searchTerm, string? searchBy);

        // ADMIN: all requests
        Task<IList<CreditFacilitiesOrLetterOfGuaranteeRequest>> GetAllAsync(
            string? searchTerm, string? searchBy, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<CreditFacilitiesOrLetterOfGuaranteeRequest?> GetByIdAsync(int id);
        Task CreateAsync(CreditFacilitiesOrLetterOfGuaranteeRequest entity);
        Task UpdateAsync(CreditFacilitiesOrLetterOfGuaranteeRequest entity);
        Task DeleteAsync(int id);
    }
}