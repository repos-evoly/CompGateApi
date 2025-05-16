public class ExternalAccountDto
{
    public string? YBCD01AB { get; set; }
    public string? YBCD01AN { get; set; }
    public string? YBCD01AS { get; set; }
}

public class ExternalAccountsResponseDetailsDto
{
    public List<ExternalAccountDto> Accounts { get; set; }
        = new();
}

public class ExternalAccountsResponseDto
{
    public ExternalAccountsResponseDetailsDto Details { get; set; }
        = new ExternalAccountsResponseDetailsDto();
}
