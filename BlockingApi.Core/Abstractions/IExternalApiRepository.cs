using System;
using System.Threading.Tasks;
using BlockingApi.Core.Dtos;

namespace BlockingApi.Core.Abstractions
{
    public interface IExternalApiRepository
    {
        Task<ExternalCustomerInfoDto?> GetCustomerInfo(string cid);
        Task<bool> BlockCustomer(
            string customerId,
            int reasonId,
            int sourceId,
            int blockedByUserId,
            DateTime? toBlockDate = null,
            string? decisionFromPublicProsecution = null,
            string? decisionFromCentralBankGovernor = null,
            string? decisionFromFIU = null,
            string? otherDecision = null);
        Task<bool> UnblockCustomer(string customerId, int unblockedByUserId);
    }
}
