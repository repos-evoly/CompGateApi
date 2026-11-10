namespace CompGateApi.Core.Errors;

public sealed class EmployeeAccountConflictException : Exception
{
    public EmployeeAccountConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
