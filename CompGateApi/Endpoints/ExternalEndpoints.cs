// // CompGateApi.Endpoints/ExternalEndpoints.cs
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net.Http;
// using System.Net.Http.Json;
// using System.Security.Claims;
// using System.Threading.Tasks;
// using CompGateApi.Core.Dtos;
// using FluentValidation;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.Logging;
// using Microsoft.AspNetCore.Mvc;
// using CompGateApi.Abstractions;
// using System.Text.Json;

// namespace CompGateApi.Endpoints
// {
//     public class ExternalEndpoints : IEndpoints
//     {
//         public void RegisterEndpoints(WebApplication app)
//         {
//             {
//                 var ext = app
//                     .MapGroup("/api/external")
//                     .WithTags("External");

//                 ext.MapGet("/accounts", LookupAccounts)
//                    .WithName("LookupAccounts")
//                    .Accepts<string>("application/json")
//                    .Produces<List<AccountDto>>(200)
//                    .Produces(400);

//                 ext.MapGet("/statement", GetStatement)
//                    .WithName("GetStatement")
//                    .Accepts<string>("application/json")
//                    .Produces<List<StatementEntryDto>>(200)
//                    .Produces(400);

//                 ext.MapPost("/transfer", PostTransfer)
//                    .WithName("PostTransfer")
//                    .Accepts<ExternalTransferDto>("application/json")
//                    .Produces<ExternalTransferResultDto>(200)
//                    .Produces(400);
//             }
//         }

//         public record AccountDto(string AccountString);

//         public record StatementEntryDto(string PostingDate, string DrCr, decimal Amount, List<string> Narratives);

//         public record ExternalTransferDto(
//             string FromAccount,
//             string ToAccount,
//             decimal Amount,
//             int CurrencyId
//         );

//         public record ExternalTransferResultDto(bool Success, string Message);

//         private static string BuildCustomer(string account13)
//         {
//             // digits 5–10 (zero‐based index 4, length 6)
//             return account13.Substring(4, 6);
//         }

//         public static async Task<IResult> LookupAccounts(
//             [FromQuery] string account,
//             ILogger<ExternalEndpoints> log)
//         {
//             if (string.IsNullOrWhiteSpace(account) || account.Length != 13)
//                 return Results.BadRequest("Account must be exactly 13 digits.");

//             var cust = BuildCustomer(account);
//             var header = new
//             {
//                 system = "MOBILE",
//                 referenceId = Guid.NewGuid().ToString("N")[..16],
//                 userName = "TEDMOB",
//                 customerNumber = cust,
//                 requestTime = DateTime.UtcNow.ToString("o"),
//                 language = "AR"
//             };
//             var details = new Dictionary<string, string> { { "@CID", cust }, { "@GETAVB", "Y" } };

//             using var client = new HttpClient();
//             var resp = await client.PostAsJsonAsync(
//                 "http://10.3.3.11:7070/api/mobile/accounts",
//                 new { Header = header, Details = details }
//             );
//             if (!resp.IsSuccessStatusCode)
//                 return Results.BadRequest($"Bank API error {resp.StatusCode}");

//             var bank = await resp.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
//             if (bank?.Details?.Accounts == null)
//                 return Results.BadRequest("Malformed response.");

//             var list = bank.Details.Accounts
//                 .ConvertAll(a => new AccountDto(
//                     (a.YBCD01AB?.Trim() ?? "") +
//                     (a.YBCD01AN?.Trim() ?? "") +
//                     (a.YBCD01AS?.Trim() ?? "")
//                 ));

//             return Results.Ok(list);
//         }

//         public static async Task<IResult> GetStatement(
//             [FromQuery] string account,
//             [FromQuery] DateTime fromDate,
//             [FromQuery] DateTime toDate,
//             ILogger<ExternalEndpoints> log)
//         {
//             if (string.IsNullOrWhiteSpace(account) || account.Length != 13)
//                 return Results.BadRequest("Account must be 13 digits.");

//             var header = new
//             {
//                 system = "MOBILE",
//                 referenceId = Guid.NewGuid().ToString("N")[..16],
//                 userName = "TEDMOB",
//                 customerNumber = account,
//                 requestTime = DateTime.UtcNow.ToString("o"),
//                 language = "AR"
//             };
//             var details = new Dictionary<string, string>{
//                 {"@TID",header.referenceId},
//                 {"@ACC",account},
//                 {"@BYDTE","Y"},
//                 {"@FDATE",fromDate.ToString("yyyyMMdd")},
//                 {"@TDATE",toDate.ToString("yyyyMMdd")},
//                 {"@BYNBR","N"},
//                 {"@NBR","0"}
//             };

//             using var client = new HttpClient { BaseAddress = new Uri("http://10.3.3.11:7070") };
//             var resp = await client.PostAsJsonAsync("/api/mobile/transactions", new { Header = header, Details = details });
//             if (!resp.IsSuccessStatusCode)
//                 return Results.BadRequest($"Bank API {resp.StatusCode}");

//             var raw = await resp.Content.ReadAsStringAsync();
//             using var doc = JsonDocument.Parse(raw);

//             if (!doc.RootElement.TryGetProperty("Details", out var d) ||
//                 !d.TryGetProperty("Transactions", out var txns) ||
//                 txns.ValueKind != JsonValueKind.Array)
//                 return Results.Ok(new List<StatementEntryDto>());

//             var result = new List<StatementEntryDto>();
//             foreach (var el in txns.EnumerateArray())
//             {
//                 var narrs = new List<string>();
//                 if (el.TryGetProperty("YBCD04NAR1", out var n1) && !string.IsNullOrWhiteSpace(n1.GetString()))
//                     narrs.Add(n1.GetString()!.Trim());
//                 if (el.TryGetProperty("YBCD04NAR2", out var n2) && !string.IsNullOrWhiteSpace(n2.GetString()))
//                     narrs.Add(n2.GetString()!.Trim());

//                 result.Add(new StatementEntryDto(
//                     PostingDate: el.GetProperty("YBCD04POD").GetString()!.Trim(),
//                     DrCr: el.GetProperty("YBCD04DRCR").GetString()!,
//                     Amount: el.GetProperty("YBCD04AMA").GetDecimal(),
//                     Narratives: narrs
//                 ));
//             }

//             return Results.Ok(result);
//         }

//         public static async Task<IResult> PostTransfer(
//             [FromBody] ExternalTransferDto dto,
//             ILogger<ExternalEndpoints> log)
//         {
//             var header = new
//             {
//                 system = "MOBILE",
//                 referenceId = Guid.NewGuid().ToString("N")[..16],
//                 userName = "TEDMOB",
//                 customerNumber = dto.ToAccount,
//                 requestTime = DateTime.UtcNow.ToString("o"),
//                 language = "AR"
//             };
//             var code = dto.CurrencyId switch { 1 => "LYD", 2 => "USD", 3 => "EUR", _ => "LYD" };
//             var details = new Dictionary<string, string>
//             {
//                 ["@TRFCCY"] = code,
//                 ["@SRCACC"] = dto.FromAccount,
//                 ["@DSTACC"] = dto.ToAccount,
//                 ["@DSTACC2"] = "",
//                 ["@TRFAMT"] = ((long)(dto.Amount * 10)).ToString("D15"),
//                 ["@APLYTRN2"] = "N",
//                 ["@TRFAMT2"] = "000000000000000",
//                 ["@NR2"] = ""
//             };

//             using var client = new HttpClient();
//             var resp = await client.PostAsJsonAsync(
//                 "http://10.3.3.11:7070/api/mobile/postTransfer",
//                 new { Header = header, Details = details }
//             );
//             if (!resp.IsSuccessStatusCode)
//                 return Results.BadRequest($"Bank API error {resp.StatusCode}");

//             var body = await resp.Content.ReadFromJsonAsync<ExternalRefundResponseDto>();
//             var ok = body?.Header?.ReturnCode?.Equals("Success", StringComparison.OrdinalIgnoreCase) == true;
//             return Results.Ok(new ExternalTransferResultDto(ok, body?.Header?.ReturnMessage ?? "Unknown"));
//         }

//         //–– remote-API DTOs ––
//         private class ExternalRefundResponseHeaderDto
//         {
//             public string ReturnCode { get; set; } = "";
//             public string ReturnMessage { get; set; } = "";
//         }
//         private class ExternalRefundResponseDto
//         {
//             public ExternalRefundResponseHeaderDto Header { get; set; }
//                 = new ExternalRefundResponseHeaderDto();
//         }
//         private class ExternalAccountDto
//         {
//             public string? YBCD01AB { get; set; }
//             public string? YBCD01AN { get; set; }
//             public string? YBCD01AS { get; set; }
//         }
//         private class ExternalAccountsResponseDetailsDto
//         {
//             public List<ExternalAccountDto> Accounts { get; set; }
//                 = new();
//         }
//         private class ExternalAccountsResponseDto
//         {
//             public ExternalAccountsResponseDetailsDto Details { get; set; }
//                 = new();
//         }
//     }
// }
