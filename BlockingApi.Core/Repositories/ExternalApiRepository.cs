using System.Net.Http.Json;
using BlockingApi.Core.Abstractions;
using BlockingApi.Core.Dtos;
using BlockingApi.Data.Context;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace BlockingApi.Core.Repositories
{
    public class ExternalApiRepository : IExternalApiRepository
    {
        private readonly HttpClient _client;
        private readonly ILogger<ExternalApiRepository> _logger;
        private readonly BlockingApiDbContext _context;

        public ExternalApiRepository(HttpClient client, BlockingApiDbContext context, ILogger<ExternalApiRepository> logger)
        {
            _client = client;
            _context = context;
            _client.BaseAddress = new Uri("http://10.3.3.11:7070/api/");
            _logger = logger;
        }

        public async Task<ExternalCustomerInfoDto?> GetCustomerInfo(string cid)
        {
            var url = "mobile/GetCustomerInfo";
            var request = new
            {
                header = new
                {
                    system = "MOBILE",
                    referenceId = $"20240607{Guid.NewGuid():N}".Substring(0, 12),
                    userName = "TEDMOB",
                    customerNumber = "102030",
                    requestTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    language = "AR"
                },
                details = new Dictionary<string, object> { { "@CID", cid } }
            };

            using HttpResponseMessage response = await _client.PostAsJsonAsync(url, request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("External API call failed with status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("External API response body: {ResponseBody}", responseBody);

            var result = JsonConvert.DeserializeObject<ExternalApiResponseDto>(responseBody);

            if (result?.Details?.CustInfo == null || result.Details.CustInfo.Count == 0)
            {
                _logger.LogWarning("External API did not return valid customer data for CID: {CID}", cid);
                return null;
            }

            return result.Details.CustInfo.FirstOrDefault();
        }



        public async Task<bool> BlockCustomer(string customerId, int reasonId, int sourceId, int blockedByUserId, DateTime? toBlockDate = null,
    string? decisionFromPublicProsecution = null, string? decisionFromCentralBankGovernor = null, string? decisionFromFIU = null, string? otherDecision = null)
        {
            var url = "mobile/CustomerLockUnlock";
            var inflag = "Y"; // "Y" to block

            // Generate a unique referenceId for each request
            var refId = $"202408{Guid.NewGuid():N}".Substring(0, 12); // Unique reference ID

            // Update the payload to use the correct key 'USRID' and unique referenceId
            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = refId, // Unique referenceId
                    userName = "TEDMOB",
                    customerNumber = customerId,
                    requestTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    language = "AR"
                },
                Details = new
                {
                    USRID = "HADI", // This should be the correct parameter key expected by the API.
                    ACCOUNT = customerId,
                    INFLAG = inflag
                }
            };

            _logger.LogInformation("🔹 Sending BlockCustomer request: {Payload}", JsonConvert.SerializeObject(payload));

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.PostAsync(url, content);


            _logger.LogInformation("🔹 BlockCustomer API Response: {StatusCode} - {ResponseContent}", response.StatusCode, response);

            if (!response.IsSuccessStatusCode) return false;

            // ✅ Ensure customer exists
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CID == customerId);
            if (customer == null) return false;

            // ✅ Check if a block already exists before inserting a new one
            var existingBlock = await _context.BlockRecords
                .Where(b => b.CustomerId == customer.Id && b.ActualUnblockDate == null)
                .OrderByDescending(b => b.BlockDate)
                .FirstOrDefaultAsync();

            if (existingBlock != null)
            {
                _logger.LogWarning("🔸 Customer {CustomerId} is already blocked. No new entry created.", customerId);
                return false; // Prevent duplicate block entry
            }

            // ✅ Insert new block record
            var blockRecord = new BlockRecord
            {
                CustomerId = customer.Id,
                ReasonId = reasonId,
                SourceId = sourceId,
                BlockDate = DateTime.UtcNow,
                ScheduledUnblockDate = toBlockDate,
                ActualUnblockDate = null,
                BlockedByUserId = blockedByUserId,
                Status = "Blocked",
                DecisionFromPublicProsecution = decisionFromPublicProsecution,
                DecisionFromCentralBankGovernor = decisionFromCentralBankGovernor,
                DecisionFromFIU = decisionFromFIU,
                OtherDecision = otherDecision
            };

            _context.BlockRecords.Add(blockRecord);

            // ✅ Ensure SaveChanges is only called once
            await _context.SaveChangesAsync();
            _logger.LogInformation("✅ Saved BlockRecord for {CustomerId}", customerId);

            return true;
        }





        public async Task<bool> UnblockCustomer(string customerId, int unblockedByUserId)
        {
            var url = "mobile/CustomerLockUnlock";
            var inflag = "N"; // "N" to unblock
            var refId = $"202408{Guid.NewGuid():N}".Substring(0, 12); // Unique reference ID

            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = refId,
                    userName = "TEDMOB",
                    customerNumber = customerId,
                    requestTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    language = "AR"
                },
                Details = new
                {
                    USRID = "HADI",
                    ACCOUNT = customerId,
                    INFLAG = inflag
                }
            };

            _logger.LogInformation("🔹 Sending UnblockCustomer request: {Payload}", JsonConvert.SerializeObject(payload));

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.PostAsync(url, content);


            _logger.LogInformation("🔹 UnblockCustomer API Response: {StatusCode} - {ResponseContent}", response.StatusCode, response);

            if (!response.IsSuccessStatusCode) return false;

            var blockRecord = await _context.BlockRecords
                .Where(b => b.Customer.CID == customerId && b.ActualUnblockDate == null)
                .OrderByDescending(b => b.BlockDate)
                .FirstOrDefaultAsync();

            if (blockRecord != null)
            {
                blockRecord.ActualUnblockDate = DateTime.UtcNow;
                blockRecord.Status = "Unblocked";
                blockRecord.UnblockedByUserId = unblockedByUserId;
                await _context.SaveChangesAsync();
            }

            return true;
        }
    }
}
