using System;

namespace CompGateApi.Core.Errors
{
    public sealed class PayrollException : Exception
    {
        public PayrollException(string message) : base(message) { }

        public PayrollException(
            string message,
            string code,
            string messageEn,
            string messageAr,
            object? details = null,
            int status = 400) : base(message)
        {
            Code = code;
            MessageEn = messageEn;
            MessageAr = messageAr;
            Details = details;
            Status = status;
        }

        public int Status { get; } = 400;
        public string? Code { get; }
        public string? MessageEn { get; }
        public string? MessageAr { get; }
        public object? Details { get; }
    }
}
