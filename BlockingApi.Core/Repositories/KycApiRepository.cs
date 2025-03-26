using BlockingApi.Abstractions;
using BlockingApi.Core.Dtos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BlockingApi.Repositories
{
    public class KycApiRepository : IKycApiRepository
    {
        private readonly HttpClient _client;
        private readonly ILogger<KycApiRepository> _logger;

        public KycApiRepository(HttpClient client, ILogger<KycApiRepository> logger)
        {
            _client = client;
            _client.BaseAddress = new Uri("http://10.3.3.11/kycapi/api/"); // Ensure the correct base URL
            _logger = logger;
        }

        public async Task<ExternalCustomerInfoDto?> SearchCustomerInKycApi(string searchTerm, string searchBy, ILogger logger, string token)
        {
            string subject = searchBy switch
            {
                "cid" => "1",
                "nationalId" => "2",
                "fullname" => "3",
                _ => throw new ArgumentException("Invalid search criteria", nameof(searchBy))
            };

            var url = $"customers/search?subject={subject}&value={searchTerm}&offset=0&limit=1&sourceApp=ccss";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using (var response = await _client.SendAsync(request))
            {
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("KYC API call failed with status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                logger.LogInformation("KYC API response body: {ResponseBody}", responseBody);

                var kycResponse = JsonConvert.DeserializeObject<KycApiResponseDto>(responseBody);

                if (kycResponse?.RowCount == 0)
                {
                    logger.LogWarning("No results found in KYC API for {SearchTerm} ({SearchBy})", searchTerm, searchBy);
                    return null;
                }

                // Map the KYC response to the ExternalCustomerInfoDto format
                var kycCustomer = kycResponse?.Result?.FirstOrDefault();
                if (kycCustomer == null)
                {
                    logger.LogWarning("No valid customer data found in KYC API response.");
                    return null;
                }

                return MapKycToExternalDto(kycCustomer);
            }
        }


        private ExternalCustomerInfoDto MapKycToExternalDto(KycCustomerInfoDto kycCustomer)
        {
            return new ExternalCustomerInfoDto
            {
                CID = kycCustomer.CustomerId,
                CNAME = kycCustomer.FullName, // Assuming this maps to the external name
                BCODE = kycCustomer.BranchId, // Assuming BCODE is BranchId in KYC API
                BNAME = kycCustomer.FullNameLT, // Assuming FullNameLT maps to Branch Name in KYC API
                NationalId = kycCustomer.NationalId // Assuming NationalId maps directly
            };
        }

    }
}
