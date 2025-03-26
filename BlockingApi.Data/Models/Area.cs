using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlockingApi.Data.Models
{
    [Table("Areas")]
    public class Area
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<Branch> Branches { get; set; } = new List<Branch>();
        public int? HeadOfSectionId { get; set; }
        [ForeignKey("HeadOfSectionId")]
        public User? HeadOfSection { get; set; }

    }
}
