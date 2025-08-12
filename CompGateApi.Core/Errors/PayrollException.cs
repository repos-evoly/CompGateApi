using System;

namespace CompGateApi.Core.Errors
{
    public sealed class PayrollException : Exception
    {
        public PayrollException(string message) : base(message) { }
    }
}
