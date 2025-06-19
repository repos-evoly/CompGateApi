namespace CompGateApi.Core.Dtos
{
    public class SettingsDto
    {


        public string CommissionAccount { get; set; } = string.Empty;

        public decimal GlobalLimit { get; set; }


    }

    public class SettingsPatchDto
    {



        public string? CommissionAccount { get; set; }

        public decimal? GlobalLimit { get; set; }
    }
}
