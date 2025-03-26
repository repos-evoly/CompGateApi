using System.ComponentModel.DataAnnotations;

namespace BlockingApi.Core.Dtos
{
    public class SearchRequestDto
    {
        [Required]
        public required string SearchTerm { get; set; }

        [Required]
        public required string SearchBy { get; set; }   // The type of search ("cid", "nationalId", "name")
        
        [Required]
        public required string KycToken { get; set; }   // The token used for the KYC API

    }
}
