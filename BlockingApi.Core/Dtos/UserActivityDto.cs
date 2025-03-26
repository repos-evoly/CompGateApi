using System;

namespace BlockingApi.Core.Dtos
{
    public class UserActivityDto
    {

        public int UserId { get; set; }
        public string Status { get; set; } = "Offline";
        public DateTime LastActivityTime { get; set; }

    }
}
