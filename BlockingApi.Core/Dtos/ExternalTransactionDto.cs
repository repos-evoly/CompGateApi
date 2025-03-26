using BlockingApi.Data.Models;

namespace BlockingApi.Core.Dtos
{
    public class ExternalTransactionRequestDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Limit { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public bool LocalCCY { get; set; }
    }

    public class ExternalTransactionApiResponseDto
    {
        public HeaderDto? Header { get; set; }
        public DetailsDto? Details { get; set; }

        public class HeaderDto
        {
            public string? System { get; set; }
            public string? ReferenceId { get; set; }
            public string? Middleware { get; set; }
            public string? SentTime { get; set; }
            public string? ReturnCode { get; set; }
            public string? ReturnMessageCode { get; set; }
            public string? ReturnMessage { get; set; }
            public string? CurCode { get; set; }
            public string? CurDescrip { get; set; }
        }

        public class DetailsDto
        {
            public List<Transaction>? Transactions { get; set; }
        }
    }
}
