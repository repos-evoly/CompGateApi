using System;
using CompGateApi.Data.Context;  // for Auditable
namespace CompGateApi.Data.Models
{
    public class CreditFacilitiesOrLetterOfGuaranteeRequest : Auditable
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CompanyId { get; set; }

        public string AccountNumber { get; set; } = null!;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Purpose { get; set; } = null!;
        public string AdditionalInfo { get; set; } = null!;
        public string Curr { get; set; } = null!;
        public string ReferenceNumber { get; set; } = null!;
        public string Type { get; set; } = null!;

        // audit / workflow
        public string Status { get; set; } = "Pending";
        public string Reason { get; set; } = string.Empty; // for admin updates

        // navigation
        public User User { get; set; } = null!;
        public Company Company { get; set; } = null!;
    }
}
