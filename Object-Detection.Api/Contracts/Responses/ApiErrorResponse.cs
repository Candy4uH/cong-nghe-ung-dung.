namespace Object_Detection.Api.Contracts.Responses;

public sealed record ApiErrorResponse(
    string ErrorCode,
    string Message,
    string? TraceId);
