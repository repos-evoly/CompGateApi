using System.Text.Json;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Errors;
using CompGateApi.Core.WalletSalary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CompGateApi.Tests;

public sealed class WalletSalaryContractTests
{
    [Theory]
    [InlineData("SAL8020260821121218", 1, "SAL8020260821001")]
    [InlineData("SAL8020260821121218", 2, "SAL8020260821002")]
    [InlineData("SLF8020260821121300", 1, "SLF8020260821001")]
    [InlineData("WSF80120260821121219", 1, "WSF8012026082001")]
    [InlineData("WSF80220260821121220", 1, "WSF8022026082001")]
    public void CoreBatchDetailId_FitsCoreFieldAndPreservesSequence(
        string reference,
        int sequence,
        string expected)
    {
        var detailId = CoreBatchDetailId.Create(reference, sequence);

        Assert.Equal(expected, detailId);
        Assert.Equal(CoreBatchDetailId.MaxLength, detailId.Length);
    }

    [Fact]
    public void CoreBatchDetailId_RejectsSequenceOutsideThreeDigitContract()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoreBatchDetailId.Create("SAL8020260821121218", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoreBatchDetailId.Create("SAL8020260821121218", 1000));
    }

    [Fact]
    public void CoreBatchDetailId_IsUniqueAcrossEverySupportedLineSequence()
    {
        var detailIds = Enumerable.Range(1, 999)
            .Select(sequence => CoreBatchDetailId.Create("SAL8020260821121218", sequence))
            .ToList();

        Assert.Equal(999, detailIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(detailIds, detailId => Assert.Equal(CoreBatchDetailId.MaxLength, detailId.Length));
    }

    [Fact]
    public void ProviderPayload_WritesExactlyOneRecipientFieldPerItem()
    {
        var request = new WalletSalaryProviderBatchRequest
        {
            Items = new()
            {
                WalletSalaryProviderContract.CreateRequestItem(5m, "evoId", "9882ebb4"),
                WalletSalaryProviderContract.CreateRequestItem(4m, "phoneNumber", "+218933333339")
            }
        };

        var json = WalletSalaryProviderContract.SerializeBatch(request);
        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("items");

        Assert.True(items[0].TryGetProperty("evoId", out _));
        Assert.False(items[0].TryGetProperty("phoneNumber", out _));
        Assert.True(items[1].TryGetProperty("phoneNumber", out _));
        Assert.False(items[1].TryGetProperty("evoId", out _));
    }

    [Fact]
    public void ProviderPayload_HashIsStableForExactJson()
    {
        const string exactJson = "{\"items\":[{\"amount\":5,\"evoId\":\"9882ebb4\"}]}";

        var first = WalletSalaryProviderContract.HashPayload(exactJson);
        var second = WalletSalaryProviderContract.HashPayload(exactJson);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, WalletSalaryProviderContract.HashPayload(exactJson + " "));
    }

    [Theory]
    [InlineData(WalletSalaryChannels.EvoWallet, "https://evo-provider.test", "fake-evo-key")]
    [InlineData(WalletSalaryChannels.BcdWallet, "https://bcd-provider.test", "fake-bcd-key")]
    public async Task ProviderClient_UsesV2BatchQueueRoute_ApiKeyOnly_AndExactBody(
        string rail,
        string baseUrl,
        string apiKey)
    {
        const string exactJson = """{"clientReference":"SAL-test-reference","items":[{"amount":5,"evoId":"9882ebb4"}]}""";
        var handler = new CaptureHandler(
            """
            {
              "batchId":"batch-1","total":1,"approved":1,"failed":0,
              "totalAmount":5,"commission":0.6,"senderCommission":0,
              "receiverCommission":0.6,"debitAmount":5,
              "items":[{"index":0,"amount":5,"evoId":"9882ebb4","commission":0.6,"status":"approved"}]
            }
            """);
        var client = new WalletSalaryProviderClient(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(new WalletSalaryOptions
            {
                Version = "v2",
                Evo = new WalletSalaryRailOptions
                {
                    Enabled = true,
                    BaseUrl = "https://evo-provider.test",
                    ApiKey = "fake-evo-key",
                    ShadowAccount = "0019000000006"
                },
                Bcd = new WalletSalaryRailOptions
                {
                    Enabled = true,
                    BaseUrl = "https://bcd-provider.test",
                    ApiKey = "fake-bcd-key",
                    ShadowAccount = "0019000000009"
                }
            }),
            NullLogger<WalletSalaryProviderClient>.Instance);

        var result = await client.SendBatchAsync(
            rail,
            exactJson,
            CancellationToken.None);

        Assert.Equal(WalletSalaryDispatchClassification.ResponseReceived, result.DispatchClassification);
        Assert.True(result.HttpSuccess);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal($"{baseUrl}/rest/v2/external-transfer/batchQueue", handler.Uri?.ToString());
        Assert.Equal(apiKey, handler.ApiKey);
        Assert.False(handler.HadAuthorizationHeader);
        Assert.Equal(exactJson, handler.Body);

        await client.GetBatchAsync(rail, "batch-1", CancellationToken.None);

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal($"{baseUrl}/rest/v2/external-transfer/batch/batch-1", handler.Uri?.ToString());
        Assert.Equal(apiKey, handler.ApiKey);
        Assert.False(handler.HadAuthorizationHeader);
        Assert.Null(handler.Body);
    }

    [Theory]
    [InlineData("{\"batchId\":\"batch-1\",\"status\":\"pending\",\"message\":\"Batch still processing\",\"total\":1}")]
    [InlineData("{\"batchId\":\"batch-1\",\"status\":\"pending\",\"total\":1,\"approved\":0,\"failed\":0,\"totalAmount\":0,\"commission\":0,\"senderCommission\":0,\"receiverCommission\":0,\"debitAmount\":0,\"items\":[]}")]
    public async Task QueuedResponse_RemainsPendingThenCompletesWithoutFundingAgain(string json)
    {
        var handler = new CaptureHandler(json);
        var client = new WalletSalaryProviderClient(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(new WalletSalaryOptions { Evo = new WalletSalaryRailOptions {
                Enabled = true, BaseUrl = "https://provider.test", ApiKey = "fake", ShadowAccount = "0019000000006"
            }}), NullLogger<WalletSalaryProviderClient>.Instance);
        var response = await client.GetBatchAsync("evoWallet", "batch-1", CancellationToken.None);
        Assert.True(response.HttpSuccess);
        Assert.Equal("pending", response.Data!.Status);
        var batch = ResponseTestBatch();
        batch.ManualReviews.Add(new SalaryWalletManualReview { ReasonCode = "PROVIDER_ITEM_MAPPING_INVALID" });
        ApplyResponse(batch, response.Data);
        Assert.Equal("pendingProvider", batch.Status);
        Assert.Equal("pendingProvider", batch.Items.Single().SalaryEntryAllocation.Status);
        Assert.False(batch.Items.Single().SalaryEntryAllocation.IsTransferred);
        Assert.NotNull(batch.NextCheckAt);
        Assert.False(batch.RetryAllowed);
        Assert.Null(batch.LastErrorCode);
        Assert.Equal("resolved", batch.ManualReviews.Single().Status);

        ApplyResponse(batch, CompletedTestResponse());
        Assert.Equal("completed", batch.Status);
        Assert.True(batch.Items.Single().SalaryEntryAllocation.IsTransferred);
        Assert.Equal(1.2m, batch.TotalCommission);
        Assert.Equal(10m, batch.SuccessfulTotalAmount);
        Assert.Null(batch.NextCheckAt);
        Assert.Equal("funded", batch.CoreFundingStatus);
        Assert.Equal("existing-funding", batch.CoreFundingReference);
        Assert.Empty(batch.Attempts);
    }

    [Theory]
    [InlineData("batch-2", "pending", 1, 0)]
    [InlineData("batch-1", "pending", 2, 0)]
    [InlineData("batch-1", "pending", 1, 1)]
    [InlineData("batch-1", "completed", 1, 0)]
    [InlineData("batch-1", "new-status", 1, 0)]
    public void InvalidEmptyResponse_StillRequiresReview(string batchId, string status, int total, int approved)
    {
        var batch = ResponseTestBatch();
        ApplyResponse(batch, new WalletSalaryProviderBatchResponse {
            BatchId = batchId, Status = status, Total = total, Approved = approved
        });
        Assert.Equal("unknown", batch.Status);
        Assert.False(batch.RetryAllowed);
        Assert.False(batch.Items.Single().SalaryEntryAllocation.IsTransferred);
    }

    [Fact]
    public void CompletedResponse_ResolvesOnlyProviderOutcomeReviews()
    {
        var batch = ResponseTestBatch();
        batch.ManualReviews.Add(new SalaryWalletManualReview { ReasonCode = "PROVIDER_ITEM_MAPPING_INVALID" });
        batch.ManualReviews.Add(new SalaryWalletManualReview { ReasonCode = "CORE_FUNDING_UNKNOWN" });
        ApplyResponse(batch, CompletedTestResponse());
        Assert.Equal("completed", batch.Status);
        var review = batch.ManualReviews.First();
        Assert.Equal("resolved", review.Status);
        Assert.NotNull(review.ResolvedAt);
        Assert.NotNull(review.ResolutionNote);
        Assert.Equal("open", batch.ManualReviews.Last().Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedZeroDebitResponse_PreservesProviderReasonAndExistingFunding(bool wrongRecipient)
    {
        var batch = ResponseTestBatch();
        batch.Items.Add(new SalaryWalletBatchItem {
            RequestIndex = 1, Amount = 10m, RecipientType = "phoneNumber", Destination = "+218912345678",
            SalaryEntryAllocation = new SalaryEntryAllocation { Amount = 10m }
        });
        batch.RequestedTotalAmount = 20m;
        var response = new WalletSalaryProviderBatchResponse {
            BatchId = "batch-1", Status = "completed", Total = 2, Failed = 2,
            Items = new() {
                new() { Index = 0, Amount = 10m, EvoId = wrongRecipient ? "wrong" : "9882ebb4",
                    Status = "failed", Error = "Merchant has insufficient available balance" },
                new() { Index = 1, Amount = 10m, PhoneNumber = "+218912345678",
                    Status = "failed", Error = "Merchant has insufficient available balance" }
            }
        };
        ApplyResponse(batch, response);
        Assert.Equal(wrongRecipient ? "unknown" : "failed", batch.Status);
        Assert.False(batch.RetryAllowed);
        Assert.Equal("funded", batch.CoreFundingStatus);
        Assert.Equal("existing-funding", batch.CoreFundingReference);
        Assert.Empty(batch.Attempts);
        if (!wrongRecipient)
        {
            Assert.Equal("Merchant has insufficient available balance", batch.LastError);
            Assert.Equal(20m, batch.FailedTotalAmount);
            Assert.Equal(0m, batch.DebitAmount);
            Assert.All(batch.Items, item => {
                Assert.False(item.SalaryEntryAllocation.IsTransferred);
                Assert.Equal("Merchant has insufficient available balance", item.SalaryEntryAllocation.TransferResultReason);
            });
        }
    }

    [Theory]
    [InlineData("SAL-test-reference")]
    [InlineData(null)]
    public void ClientReference_IsTopLevelAndSurvivesStoredRequestReconstruction(string? reference)
    {
        var request = new WalletSalaryProviderBatchRequest {
            ClientReference = reference,
            Items = new() { WalletSalaryProviderContract.CreateRequestItem(10m, "evoId", "9882ebb4") }
        };
        var saved = WalletSalaryProviderContract.SerializeBatch(request);
        using var doc = JsonDocument.Parse(saved);
        Assert.False(doc.RootElement.GetProperty("items")[0].TryGetProperty("clientReference", out _));
        if (reference != null) Assert.Equal(reference, doc.RootElement.GetProperty("clientReference").GetString());
        else Assert.False(doc.RootElement.TryGetProperty("clientReference", out _));
        var restored = JsonSerializer.Deserialize<WalletSalaryProviderBatchRequest>(saved)!;
        var reconstructed = new WalletSalaryProviderBatchRequest {
            ClientReference = restored.ClientReference,
            Items = new() { WalletSalaryProviderContract.CreateRequestItem(10m, "evoId", "9882ebb4") }
        };
        Assert.Equal(saved, WalletSalaryProviderContract.SerializeBatch(reconstructed));
        Assert.Equal(WalletSalaryProviderContract.HashPayload(saved),
            WalletSalaryProviderContract.HashPayload(WalletSalaryProviderContract.SerializeBatch(reconstructed)));
    }

    private static SalaryWalletBatch ResponseTestBatch() => new() {
        ProviderBatchId = "batch-1", CoreFundingStatus = "funded", CoreFundingReference = "existing-funding",
        RequestedTotalAmount = 10m,
        Items = new List<SalaryWalletBatchItem> { new() {
            RequestIndex = 0, Amount = 10m, RecipientType = "evoId", Destination = "9882ebb4",
            SalaryEntryAllocation = new SalaryEntryAllocation { Amount = 10m }
        }}
    };

    private static WalletSalaryProviderBatchResponse CompletedTestResponse() => new() {
        BatchId = "batch-1", Status = "completed", Total = 1, Approved = 1,
        TotalAmount = 10m, Commission = 1.2m, ReceiverCommission = 1.2m, DebitAmount = 10m,
        Items = new() { new() { Index = 0, Amount = 10m, EvoId = "9882ebb4", Commission = 1.2m,
            Status = "approved", TransactionId = "transaction-1" }}
    };

    private static void ApplyResponse(SalaryWalletBatch batch, WalletSalaryProviderBatchResponse response)
    {
        // Exercise response application entirely in memory: no database or payment transport.
        var service = new WalletSalaryService(null!, null!, null!, Options.Create(new WalletSalaryOptions()),
            null!, NullLogger<WalletSalaryService>.Instance);
        typeof(WalletSalaryService).GetMethod("ApplyProviderResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(service, new object?[] { batch, response, JsonSerializer.Serialize(response), null });
    }

    [Theory]
    [InlineData("approved", WalletSalaryItemOutcome.Completed)]
    [InlineData("completed", WalletSalaryItemOutcome.Completed)]
    [InlineData("declined", WalletSalaryItemOutcome.Failed)]
    [InlineData("processing", WalletSalaryItemOutcome.Pending)]
    [InlineData("provider-new-status", WalletSalaryItemOutcome.Unknown)]
    [InlineData(null, WalletSalaryItemOutcome.Unknown)]
    public void ProviderStatusMapping_IsCentralizedAndFailClosed(
        string? status,
        WalletSalaryItemOutcome expected) =>
        Assert.Equal(expected, WalletSalaryService.ClassifyProviderItemStatus(status));

    [Theory]
    [InlineData("pendingProvider", true)]
    [InlineData("unknown", true)]
    [InlineData("completed", false)]
    [InlineData("failed", false)]
    [InlineData("partiallyCompleted", false)]
    [InlineData("dispatching", false)]
    public void ProviderRefresh_IsOnlyAllowedForNonterminalReconciliationStates(
        string status,
        bool expected) =>
        Assert.Equal(expected, WalletSalaryService.IsRefreshableStatus(status));

    [Fact]
    public void CompanyDebitAccount_RequiresExactOwnedThirteenDigitAccount()
    {
        Assert.Equal(
            "0019738710001",
            SalaryAllocationRules.ValidateCompanyDebitAccount(" 0019738710001 ", "738710"));

        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.ValidateCompanyDebitAccount("19738710001", "738710"));
        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.ValidateCompanyDebitAccount("0019738710001", "798000"));
    }

    [Fact]
    public void AllocationSnapshots_DefaultToFullBankSalary()
    {
        var employee = Employee(accountNumber: "0019738710001");
        var entry = new SalaryEntryUpsertDto { EmployeeId = employee.Id, Salary = 10.125m };

        var allocation = Assert.Single(
            SalaryAllocationRules.BuildAllocationSnapshots(employee, entry, "LYD"));

        Assert.Equal(WalletSalaryChannels.BankAccount, allocation.PaymentChannel);
        Assert.Equal("0019738710001", allocation.Destination);
        Assert.Equal(10.125m, allocation.Amount);
    }

    [Fact]
    public void AllocationSnapshots_UseSavedEmployeeDefaultsWhenRequestOmitsAllocations()
    {
        var employee = Employee(
            accountNumber: "0019738710001",
            evoWallet: "9882ebb4",
            evoType: "evoId",
            bcdWallet: "+218945535786",
            bcdType: "phoneNumber",
            phone: "+218945535786");
        employee.BankAllocationAmount = 50m;
        employee.EvoAllocationAmount = 20m;
        employee.BcdAllocationAmount = 30m;
        var entry = new SalaryEntryUpsertDto { EmployeeId = employee.Id, Salary = 100m };

        var allocations = SalaryAllocationRules.BuildAllocationSnapshots(employee, entry, "LYD");

        Assert.Collection(
            allocations,
            bank =>
            {
                Assert.Equal(WalletSalaryChannels.BankAccount, bank.PaymentChannel);
                Assert.Equal(50m, bank.Amount);
            },
            evo =>
            {
                Assert.Equal(WalletSalaryChannels.EvoWallet, evo.PaymentChannel);
                Assert.Equal(20m, evo.Amount);
            },
            bcd =>
            {
                Assert.Equal(WalletSalaryChannels.BcdWallet, bcd.PaymentChannel);
                Assert.Equal(30m, bcd.Amount);
            });
    }

    [Fact]
    public void AllocationSnapshots_SupportBankAndBothWalletRails()
    {
        var employee = Employee(
            accountNumber: "0019738710001",
            evoWallet: "9882ebb4",
            evoType: "evoId",
            bcdWallet: "+218933333339",
            bcdType: "phoneNumber",
            phone: "+218933333339");
        var entry = new SalaryEntryUpsertDto
        {
            EmployeeId = employee.Id,
            Salary = 10m,
            Allocations = new()
            {
                new() { PaymentChannel = "bankAccount", Amount = 2m },
                new() { PaymentChannel = "evoWallet", Amount = 3m },
                new() { PaymentChannel = "bcdWallet", Amount = 5m }
            }
        };

        var allocations = SalaryAllocationRules.BuildAllocationSnapshots(employee, entry, "LYD");

        Assert.Equal(3, allocations.Count);
        Assert.Equal(10m, allocations.Sum(x => x.Amount));
        Assert.Equal("evoId", allocations.Single(x => x.PaymentChannel == "evoWallet").RecipientType);
        Assert.Equal("phoneNumber", allocations.Single(x => x.PaymentChannel == "bcdWallet").RecipientType);
    }

    [Fact]
    public void AllocationSnapshots_CanonicalizeStoredPhoneDestinationFromEmployeePhone()
    {
        var employee = Employee(
            bcdWallet: "0945-535-786",
            bcdType: "phoneNumber",
            phone: "945 535 786");
        var entry = new SalaryEntryUpsertDto
        {
            EmployeeId = employee.Id,
            Salary = 10m,
            Allocations = new()
            {
                new() { PaymentChannel = "bcdWallet", Amount = 10m }
            }
        };

        var allocation = Assert.Single(
            SalaryAllocationRules.BuildAllocationSnapshots(employee, entry, "LYD"));

        Assert.Equal("+218945535786", allocation.Destination);
        Assert.Equal("phoneNumber", allocation.RecipientType);
    }

    [Fact]
    public void AllocationSnapshots_RejectStoredPhoneDestinationThatDiffersFromEmployeePhone()
    {
        var employee = Employee(
            bcdWallet: "+218933333339",
            bcdType: "phoneNumber",
            phone: "+218945535786");
        var entry = new SalaryEntryUpsertDto
        {
            EmployeeId = employee.Id,
            Salary = 10m,
            Allocations = new()
            {
                new() { PaymentChannel = "bcdWallet", Amount = 10m }
            }
        };

        var error = Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.BuildAllocationSnapshots(employee, entry, "LYD"));

        Assert.Contains("must match the employee phone number", error.Message);
    }

    [Fact]
    public void AllocationSnapshots_RejectDuplicateRailAndMismatchedTotal()
    {
        var employee = Employee(accountNumber: "0019738710001");
        var duplicate = new SalaryEntryUpsertDto
        {
            EmployeeId = employee.Id,
            Salary = 10m,
            Allocations = new()
            {
                new() { PaymentChannel = "bankAccount", Amount = 5m },
                new() { PaymentChannel = "bankAccount", Amount = 5m }
            }
        };
        var mismatch = new SalaryEntryUpsertDto
        {
            EmployeeId = employee.Id,
            Salary = 10m,
            Allocations = new()
            {
                new() { PaymentChannel = "bankAccount", Amount = 9m }
            }
        };

        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.BuildAllocationSnapshots(employee, duplicate, "LYD"));
        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.BuildAllocationSnapshots(employee, mismatch, "LYD"));
    }

    [Fact]
    public void EmployeeDestination_IsRequiredOnlyWhenSendSalaryIsEnabled()
    {
        var disabled = new EmployeeCreateDto { Name = "Employee", SendSalary = false };
        SalaryAllocationRules.NormalizeAndValidateEmployee(disabled);

        var enabled = new EmployeeCreateDto { Name = "Employee", SendSalary = true };
        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(enabled));
    }

    [Theory]
    [InlineData("945535786")]
    [InlineData("0945535786")]
    [InlineData("+218945535786")]
    [InlineData("218945535786")]
    [InlineData("00218 945-535-786")]
    [InlineData("+218.945.535.786")]
    [InlineData(" +218 (945) 535-786 ")]
    public void LibyanPhoneNumber_NormalizesSupportedInputFormats(string rawPhone)
    {
        Assert.True(LibyanPhoneNumber.TryNormalize(rawPhone, out var normalized));
        Assert.Equal("+218945535786", normalized);
    }

    [Fact]
    public void EmployeePhoneWallets_AreBothDerivedFromEmployeePhone()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Phone = "0945 535-786",
            Salary = 100m,
            EvoWallet = "945535786",
            EvoWalletRecipientType = "phoneNumber",
            BcdWallet = "+218 (945) 535 786",
            BcdWalletRecipientType = "phoneNumber",
            EvoAllocationAmount = 40m,
            BcdAllocationAmount = 60m,
            SendSalary = true
        };

        SalaryAllocationRules.NormalizeAndValidateEmployee(dto);

        Assert.Equal("+218945535786", dto.Phone);
        Assert.Equal("+218945535786", dto.EvoWallet);
        Assert.Equal("+218945535786", dto.BcdWallet);
        Assert.Equal("phoneNumber", dto.EvoWalletRecipientType);
        Assert.Equal("phoneNumber", dto.BcdWalletRecipientType);
    }

    [Fact]
    public void EmployeePhoneWallet_IsDerivedWhenWalletDestinationIsEmpty()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Phone = "945535786",
            Salary = 100m,
            EvoWalletRecipientType = "phoneNumber",
            EvoAllocationAmount = 100m,
            SendSalary = true
        };

        SalaryAllocationRules.NormalizeAndValidateEmployee(dto);

        Assert.Equal("+218945535786", dto.Phone);
        Assert.Equal("+218945535786", dto.EvoWallet);
    }

    [Fact]
    public void EmployeePhoneWallet_RejectsDifferentDirectApiDestination()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Phone = "945535786",
            Salary = 100m,
            EvoWallet = "+218933333339",
            EvoWalletRecipientType = "phoneNumber",
            EvoAllocationAmount = 100m,
            SendSalary = true
        };

        var error = Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(dto));

        Assert.Contains("must match the employee phone number", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("+218123456789")]
    [InlineData("+21894553578X")]
    public void EmployeePhoneAllocation_RequiresValidLibyanEmployeePhone(string? employeePhone)
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Phone = employeePhone,
            Salary = 100m,
            EvoWalletRecipientType = "phoneNumber",
            EvoAllocationAmount = 100m,
            SendSalary = true
        };

        var error = Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(dto));

        Assert.Contains("requires a valid Libyan employee phone number", error.Message);
    }

    [Fact]
    public void EmployeeUnusedPhoneRail_IsClearedWhenEmployeePhoneIsUnavailable()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Phone = null,
            Salary = 100m,
            AccountNumber = "0015798000001",
            BankAllocationAmount = 100m,
            EvoWallet = "+218933333339",
            EvoWalletRecipientType = "phoneNumber",
            EvoAllocationAmount = 0m,
            SendSalary = true
        };

        SalaryAllocationRules.NormalizeAndValidateEmployee(dto);

        Assert.Null(dto.EvoWallet);
        Assert.Null(dto.EvoWalletRecipientType);
    }

    [Fact]
    public void EmployeeEvoIdDestination_RemainsIndependentOfEmployeePhone()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Phone = "not-a-wallet-phone",
            Salary = 100m,
            EvoWallet = "9882ebb4",
            EvoWalletRecipientType = "evoId",
            EvoAllocationAmount = 100m,
            SendSalary = true
        };

        SalaryAllocationRules.NormalizeAndValidateEmployee(dto);

        Assert.Equal("not-a-wallet-phone", dto.Phone);
        Assert.Equal("9882ebb4", dto.EvoWallet);
        Assert.Equal("evoId", dto.EvoWalletRecipientType);
    }

    [Fact]
    public void EmployeeDefaultAllocations_AcceptExactSalarySplit()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Salary = 100m,
            AccountNumber = "0015798000001",
            EvoWallet = "9882ebb4",
            EvoWalletRecipientType = "evoId",
            Phone = "+218945535786",
            BcdWallet = "+218945535786",
            BcdWalletRecipientType = "phoneNumber",
            BankAllocationAmount = 50m,
            EvoAllocationAmount = 20m,
            BcdAllocationAmount = 30m,
            SendSalary = true
        };

        SalaryAllocationRules.NormalizeAndValidateEmployee(dto);

        Assert.Equal(50m, dto.BankAllocationAmount);
        Assert.Equal(20m, dto.EvoAllocationAmount);
        Assert.Equal(30m, dto.BcdAllocationAmount);
    }

    [Fact]
    public void EmployeeDefaultAllocations_RejectMismatchWhenSalaryIsEnabled()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Salary = 100m,
            AccountNumber = "0015798000001",
            BankAllocationAmount = 99m,
            SendSalary = true
        };

        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(dto));
    }

    [Fact]
    public void EmployeeDefaultAllocations_RejectZeroSalaryWhenSalaryIsEnabled()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            AccountNumber = "0015798000001",
            Salary = 0m,
            BankAllocationAmount = 0m,
            SendSalary = true
        };

        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(dto));
    }

    [Fact]
    public void EmployeeDefaultAllocations_RejectAmountWithoutDestination()
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            Salary = 100m,
            EvoAllocationAmount = 100m,
            SendSalary = false
        };

        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(dto));
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("0.0001")]
    public void EmployeeDefaultAllocations_RejectNegativeOrMoreThanThreeDecimals(string rawAmount)
    {
        var dto = new EmployeeCreateDto
        {
            Name = "Employee",
            AccountNumber = "0015798000001",
            BankAllocationAmount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture)
        };

        Assert.Throws<PayrollException>(() =>
            SalaryAllocationRules.NormalizeAndValidateEmployee(dto));
    }

    private static Employee Employee(
        string? accountNumber = null,
        string? evoWallet = null,
        string? evoType = null,
        string? bcdWallet = null,
        string? bcdType = null,
        string? phone = null) => new()
        {
            Id = 7,
            Name = "Employee Seven",
            Phone = phone,
            AccountNumber = accountNumber,
            AccountType = "account",
            EvoWallet = evoWallet,
            EvoWalletRecipientType = evoType,
            BcdWallet = bcdWallet,
            BcdWalletRecipientType = bcdType
        };

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? ApiKey { get; private set; }
        public bool HadAuthorizationHeader { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Method = request.Method;
            ApiKey = request.Headers.TryGetValues("x-api-key", out var values)
                ? values.Single()
                : null;
            HadAuthorizationHeader = request.Headers.Authorization is not null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        }
    }
}
