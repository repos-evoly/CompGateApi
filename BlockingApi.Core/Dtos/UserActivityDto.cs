using System;

namespace BlockingApi.Core.Dtos
{
    public class UserActivityDto
    {
        public int UserId { get; set; }
        public string Status { get; set; } = "Offline";
        public DateTimeOffset LastActivityTime { get; set; }

        // Enriched from the auth system:
        public DateTimeOffset? LastLogin { get; set; }
        public DateTimeOffset? LastLogout { get; set; }
    }


}
