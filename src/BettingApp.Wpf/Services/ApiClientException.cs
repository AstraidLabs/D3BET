using System.Net;

namespace BettingApp.Wpf.Services;

public sealed class ApiClientException(
    string userMessage,
    HttpStatusCode? statusCode = null,
    string? traceId = null,
    bool isTransient = false,
    Exception? innerException = null) : Exception(userMessage, innerException)
{
    public string UserMessage { get; } = userMessage;

    public HttpStatusCode? StatusCode { get; } = statusCode;

    public string? TraceId { get; } = traceId;

    public bool IsTransient { get; } = isTransient;
}
