using System;
using System.Collections.Generic;

namespace CompGateApi.Core.Dtos
{
    /// <summary>
    /// Represents a single branch entry returned by the KYC API.
    /// </summary>
    public class BranchDto
    {
        public string BranchNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string BranchMnemonic { get; set; } = string.Empty;
    }

    /// <summary>
    /// Wraps the "Details" section of the KYC API response.
    /// </summary>
    public class BranchesDetailsDto
    {
        public List<BranchDto> Branches { get; set; } = new List<BranchDto>();
    }

    /// <summary>
    /// Wraps the "Header" section of the KYC API response.
    /// </summary>
    public class HeaderDto
    {
        public string System { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public DateTimeOffset SentTime { get; set; }
        public string MiddlewareId { get; set; } = string.Empty;
        public string ReturnCode { get; set; } = string.Empty;
        public string ReturnMessageCode { get; set; } = string.Empty;
        public string ReturnMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Top-level DTO matching the KYC API's JSON structure for getting active branches.
    /// </summary>
    public class ActiveBranchesResponseDto
    {
        public HeaderDto Header { get; set; } = new HeaderDto();
        public BranchesDetailsDto Details { get; set; } = new BranchesDetailsDto();
    }
}
