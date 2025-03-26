namespace BlockingApi.Core.Dtos
{
    public class TransactionActionDto
    {
        public int TransactionId { get; set; }
        public required string Action { get; set; }
    }


    public class EscalateTransactionDto
    {
        public int TransactionId { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
    }

    public class ReturnEscalationDto
    {
        public int TransactionId { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
    }



    public class CreateTransactionDto
    {
        public string BranchCode { get; set; } = string.Empty;
        public string Basic { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string InputBranch { get; set; } = string.Empty;
        public string DC { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CCY { get; set; } = string.Empty;
        public string InputBranchNo { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int PostingDate { get; set; }
        public string Nr1 { get; set; } = string.Empty;
        public string? Nr2 { get; set; }
        public DateTime Timestamp { get; set; }
        // Optional status; if not provided, default to "Pending"
        public string? Status { get; set; }
        // Owner provided from the request (as string representation of userId)
        public string Initiator { get; set; } = string.Empty;
        // Optional: current party handling the transaction
        public string? CurrentParty { get; set; }
    }

    public class BatchAddTransactionsDto
    {
        public List<CreateTransactionDto> Transactions { get; set; } = new List<CreateTransactionDto>();
    }

    public class TransactionWithFlowDto
    {
        public TransactionDto? Transaction { get; set; }
        public List<TransactionFlowDto>? TransactionFlows { get; set; }
    }

    public class TransactionDto
    {
        public int Id { get; set; }
        public string? BranchCode { get; set; }
        public string? BranchName { get; set; }
        public string? Basic { get; set; }
        public string? Suffix { get; set; }
        public string? InputBranch { get; set; }
        public string? DC { get; set; }
        public decimal Amount { get; set; }
        public string? CCY { get; set; }
        public string? InputBranchNo { get; set; }
        public int PostingDate { get; set; }
        public string? Nr1 { get; set; }
        public string? Nr2 { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Status { get; set; }
        public string? Owner { get; set; }
        public string? CurrentParty { get; set; }
    }

    public class TransactionFlowDto
    {
        public int Id { get; set; }
        public string? Action { get; set; }
        public DateTime ActionDate { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public string? Remark { get; set; }
        public bool CanReturn { get; set; }
        public string? FromUserName { get; set; }
        public string? ToUserName { get; set; }
    }


}
