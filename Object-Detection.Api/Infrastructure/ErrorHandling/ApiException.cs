namespace Object_Detection.Api.Infrastructure.ErrorHandling;

public sealed class ApiException : Exception
{
    public ApiException(string errorCode, string message, int statusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }

    public int StatusCode { get; }
}
