using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Object_Detection.Api.Contracts.Responses;

namespace Object_Detection.Api.Infrastructure.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            ApiException apiEx => (apiEx.StatusCode, apiEx.ErrorCode, apiEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "Unexpected server error.")
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = new ApiErrorResponse(
            ErrorCode: errorCode,
            Message: message,
            TraceId: httpContext.TraceIdentifier);

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions), cancellationToken);
        return true;
    }
}
