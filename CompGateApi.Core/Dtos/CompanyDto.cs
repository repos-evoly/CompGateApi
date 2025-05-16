// CompGateApi.Core.Dtos/KycDto.cs
using System.ComponentModel.DataAnnotations;
using CompGateApi.Data.Models;

namespace CompGateApi.Core.Dtos
{
    public class KycDto
    {
        public string companyId { get; set; } = string.Empty;
        public string branchId { get; set; } = string.Empty;
        public string legalCompanyName { get; set; } = string.Empty;
        public string legalCompanyNameLT { get; set; } = string.Empty;
        public string mobile { get; set; } = string.Empty;
        public string nationality { get; set; } = string.Empty;
        public string nationalityEN { get; set; } = string.Empty;
        public string nationalityCode { get; set; } = string.Empty;
        public string? street { get; set; }
        public string? district { get; set; }
        public string? buildingNumber { get; set; }
        public string? city { get; set; }
    }
}

// CompGateApi.Core.Dtos/CompanyRegistrationDto.cs
namespace CompGateApi.Core.Dtos
{
    public class CompanyRegistrationDto
    {
        [Required, StringLength(6, MinimumLength = 6)]
        public string CompanyId { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        [Required]
        public int RoleId { get; set; }
    }
}

// CompGateApi.Core.Dtos/CompanyStatusUpdateDto.cs
namespace CompGateApi.Core.Dtos
{
    public class CompanyStatusUpdateDto
    {
        [Required]
        public KycStatus Status { get; set; }

        [MaxLength(500)]
        public string? Message { get; set; }
    }
}

// CompGateApi.Core.Dtos/CompanyEmployeeRegistrationDto.cs

namespace CompGateApi.Core.Dtos
{
    public class CompanyEmployeeRegistrationDto
    {
        /// <summary>Must match a 6-digit company code in the route</summary>
        // we pull CompanyId from the URL, so we don’t need it here

        [Required, MaxLength(150)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(15)]
        public string Phone { get; set; } = string.Empty;

        /// <summary>One of your predefined role IDs (e.g. Accountant = 6, etc.)</summary>
        [Required]
        public int RoleId { get; set; }
    }
}

namespace CompGateApi.Core.Dtos
{
    public class CompanyEmployeeDetailsDto
    {
        /// <summary>Local DB PK</summary>
        public int Id { get; set; }

        /// <summary>Auth system’s userId</summary>
        public int AuthUserId { get; set; }

        /// <summary>The 6-digit CompanyId from the route</summary>
        public string CompanyId { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        /// <summary>The RoleId you assigned (e.g. 6 = Accountant)</summary>
        public int RoleId { get; set; }

        /// <summary>A flat list of permission keys that this user has</summary>
        public List<string> Permissions { get; set; } = new List<string>();
    }
}

namespace CompGateApi.Core.Dtos
{
    public class CompanyListDto
    {
        public string CompanyId { get; set; } = string.Empty;
        public string? BranchId { get; set; }
        public string? LegalNameEn { get; set; }
        public string? LegalNameLt { get; set; }
        public KycStatus Status { get; set; }
        public string? StatusMessage { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}