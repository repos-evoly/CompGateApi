using System.Threading;
using System.Threading.Tasks;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Abstractions
{
    /// <summary>
    /// Generic helper to call core for service debits (e.g., checkbooks).
    /// Uses CompanyGatewayPostTransfer for debit (with or without second leg),
    /// and flexReverseTransfer for refunds.
    /// </summary>
    public interface IGenericTransferRepository
    {
        /// <summary>
        /// Debit 'fromAccount' → 'toAccount' with optional second leg OFF/ON based on applySecondLeg.
        /// - CompanyGatewayPostTransfer
        /// - Currency code explicit (e.g., "LYD")
        /// - DTCD/CTCD codes come from Pricing
        /// - If applySecondLeg=false: APLYTRN2="N", TRFAMT2=0 (no commission)
        /// - Returns persisted TransferRequest and the BankReference.
        /// </summary>
        Task<(bool Success, string? Error, TransferRequest? Entity, string? BankReference)>
            DebitForServiceAsync(
                int userId,
                int companyId,
                int servicePackageId,
                int trxCategoryId,
                string fromAccount,
                string toAccount,
                decimal amount,
                string description,
                string currencyCode,
                string? dtc,
                string? ctc,
                string? dtc2,
                string? ctc2,
                bool applySecondLeg,
                string? narrativeOverride,
                CancellationToken ct = default);

        /// <summary>
        /// Reverse the original transfer by original bank reference (TRFREFORG).
        /// - flexReverseTransfer
        /// - No second leg (APLYTRN2="N" and TRFAMT2=0)
        /// - srcAcc should be the account that originally RECEIVED the money (fees),
        ///   dstAcc should be the account that originally SENT the money (customer).
        /// </summary>
        Task<(bool Success, string? Error)>
            RefundByOriginalRefAsync(
                string originalBankRef,
                string currencyCode,
                string srcAcc,
                string dstAcc,
                string srcAcc2,
                string dstAcc2,
                decimal amount,
                string note,
                CancellationToken ct = default);
    }
}
