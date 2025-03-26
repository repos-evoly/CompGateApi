using BlockingApi.Core.Dtos;
using Microsoft.Extensions.Logging;

namespace BlockingApi.Abstractions
{
    public interface IKycApiRepository
    {
        Task<ExternalCustomerInfoDto?> SearchCustomerInKycApi(string searchTerm, string searchBy, ILogger logger, string token);
    }
}
