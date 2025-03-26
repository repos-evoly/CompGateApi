namespace BlockingApi.Core.Dtos
{
    public class ExternalApiResponseDto
    {
        public ExternalApiResponseHeader? Header { get; set; }
        public ExternalApiResponseDetails? Details { get; set; }
        public int RowCount { get; internal set; }
    }

    public class ExternalApiResponseHeader
    {
        public string? ReturnCode { get; set; }
        public string? ReturnMessage { get; set; }
    }

    public class ExternalApiResponseDetails
    {
        public List<ExternalCustomerInfoDto>? CustInfo { get; set; }
    }

    public class ExternalCustomerInfoDto
    {
        public string? customerId;

        public string? CID { get; set; }
        public string? CNAME { get; set; }
        public string? BCODE { get; set; }
        public string? BNAME { get; set; }
        public string? STCOD { get; set; }
        public string? NationalId { get; internal set; }
        public string? LastName { get; set; }
    }

    public class KycApiResponseDto
    {
        public int RowCount { get; set; }
        public List<KycCustomerInfoDto>? Result { get; set; }
    }

    public class KycCustomerInfoDto
    {
        public string? CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? FullNameLT { get; set; }
        public string? NationalId { get; set; }
        public string? BranchId { get; set; }
    }
}
