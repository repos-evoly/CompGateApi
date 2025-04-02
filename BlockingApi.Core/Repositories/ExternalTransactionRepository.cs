using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlockingApi.Data.Models;
using BlockingApi.Core.Dtos;
using System.Collections.Generic;
using System.Net.Http.Headers;
using BlockingApi.Data.Abstractions;

namespace BlockingApi.Data.Repositories
{
    public class ExternalTransactionRepository : IExternalTransactionRepository
    {
        private readonly HttpClient _httpClient;

        public ExternalTransactionRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Method to get external transactions from the bank API
        public async Task<List<Transaction>> GetExternalTransactionsAsync(int fromDate, int toDate, int limit, string branchCode, bool localCCY)
        {
            // Construct the request body for the external API
            var requestBody = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = $"202503121234AT10",  // This should ideally be dynamically generated
                    userName = "TEDMOB",
                    customerNumber = "102030",
                    requestTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    language = "AR"
                },
                Details = new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    Limit = limit,
                    BranchCode = branchCode,
                    LocalCCY = localCCY ? "Y" : "N" // Convert to Y/N based on the flag
                }
            };

            // Log the request body for debugging purposes
            var requestJson = JsonSerializer.Serialize(requestBody);
            Console.WriteLine($"Sending request body: {requestJson}");

            // Create HTTP content explicitly
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            // Ensure the request content type is correct
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Make the POST request to the external API
            var response = await _httpClient.PostAsync("http://10.3.3.11:7070/api/mobile/GetAuditTrans", content);

            // Check if the request was successful
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Response: {responseContent}");

                var apiResponse = JsonSerializer.Deserialize<ExternalTransactionApiResponseDto>(responseContent);

                var transactions = apiResponse?.Details?.Transactions;

                return transactions ?? new List<Transaction>();
            }
            else
            {

                Console.WriteLine($"Error: {response.StatusCode}");

                return new List<Transaction>();
            }
        }
    }
}
