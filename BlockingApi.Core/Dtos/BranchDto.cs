using System.ComponentModel.DataAnnotations;

namespace BlockingApi.Core.Dtos
{
    public class BranchDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string CABBN { get; set; } = string.Empty;

        public int AreaId { get; set; }
    }


    public class EditBranchDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(15)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public int AreaId { get; set; }
    }

}
