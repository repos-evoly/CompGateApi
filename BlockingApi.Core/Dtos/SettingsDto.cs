namespace BlockingApi.Core.Dtos
{
    public class SettingsDto
    {
        public int? TransactionAmount { get; set; }
        public int? TransactionAmountForeign { get; set; }
        public string? TransactionTimeTo { get; set; }
        public string? TimeToIdle { get; set; }
    }

    public class SettingsPatchDto
    {
        public int? TransactionAmount { get; set; }
        public int? TransactionAmountForeign { get; set; }
        public string? TransactionTimeTo { get; set; }
        public string? TimeToIdle { get; set; }
    }
}
