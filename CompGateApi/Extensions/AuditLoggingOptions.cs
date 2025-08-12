using System;
using Microsoft.AspNetCore.Http;

namespace CompGateApi.Infrastructure
{
    public sealed class AuditLoggingOptions
    {
        /// <summary>Capture and store request body (for POST/PUT/PATCH).</summary>
        public bool CaptureRequestBody { get; set; } = true;

        /// <summary>Capture and store response body.</summary>
        public bool CaptureResponseBody { get; set; } = true;

        /// <summary>Maximum characters from request/response bodies to store.</summary>
        public int MaxBodyChars { get; set; } = 4000;

        /// <summary>Hook to enrich ExtrasJson with custom data per-request.</summary>
        public Func<HttpContext, object?>? EnrichExtras { get; set; }
    }
}
