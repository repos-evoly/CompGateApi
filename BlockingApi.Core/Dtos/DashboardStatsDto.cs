namespace BlockingApi.Core.Dtos
{
    public class DashboardStatsDto
    {
        public int BlockedAccounts { get; set; }
        public int FlaggedTransactions { get; set; }
        public int BlockedUsersToday { get; set; }
        public int HighValueTransactions { get; set; }
    }
}
