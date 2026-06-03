public sealed class SalaryApiErrorResponseDto
{
    public bool Success { get; init; }
    public int Status { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string MessageEn { get; init; } = string.Empty;
    public string MessageAr { get; init; } = string.Empty;
    public object? Details { get; init; }
}
