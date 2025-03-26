using System.ComponentModel.DataAnnotations;

namespace BlockingApi.Core.Dtos
{
    public class UnblockCustomerDto
    {
        [Required]
        public string CustomerId { get; set; } = string.Empty;
        
         public int UnblockedByUserId { get; set; } = 0;
    }
}
