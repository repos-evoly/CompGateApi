using System;
using System.Collections.Generic;

namespace CompGateApi.Core.Dtos
{
    // Internal output DTOs
    public class BranchDto
    {
        public string BranchNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string BranchMnemonic { get; set; } = string.Empty;
    }

    public class BranchesDetailsDto
    {
        public List<BranchDto> Branches { get; set; } = new();
    }

    public class HeaderDto
    {
        public string System { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public string SentTime { get; set; } = string.Empty;
        public string MiddlewareId { get; set; } = string.Empty;
        public string ReturnCode { get; set; } = string.Empty;
        public string ReturnMessageCode { get; set; } = string.Empty;
        public string ReturnMessage { get; set; } = string.Empty;
    }

    public class ActiveBranchesResponseDto
    {
        public HeaderDto Header { get; set; } = new();
        public BranchesDetailsDto Details { get; set; } = new();
    }

    // External mapping DTOs
    public class ExternalBranch
    {
        public string CABBN { get; set; } = string.Empty;
        public string CABRN { get; set; } = string.Empty;
        public string CABRNM { get; set; } = string.Empty;
    }

    public class ExternalBranchesDetailsDto
    {
        public List<ExternalBranch> Branches { get; set; } = new();
    }

    public class ExternalHeaderDto
    {
        public string System { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public string Middleware { get; set; } = string.Empty;
        public string SentTime { get; set; } = string.Empty;
        public string ReturnCode { get; set; } = string.Empty;
        public string ReturnMessageCode { get; set; } = string.Empty;
        public string ReturnMessage { get; set; } = string.Empty;
        public string CurCode { get; set; } = string.Empty;
        public string CurDescrip { get; set; } = string.Empty;
    }

    public class ExternalActiveBranchesResponseDto
    {
        public ExternalHeaderDto Header { get; set; } = new();
        public ExternalBranchesDetailsDto Details { get; set; } = new();
    }
}
