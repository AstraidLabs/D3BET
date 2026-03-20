using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BettingApp.Application.Models;
using BettingApp.Domain.Entities;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Services;

public sealed class OperationsApiClient(
    OperatorAuthOptions authOptions,
    OperatorAuthService operatorAuthService)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/operations/dashboard");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<DashboardDto>(response, cancellationToken);
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/operations/settings");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<AppSettings>(response, cancellationToken);
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, "/api/operations/settings", JsonContent.Create(settings));
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<CustomerDisplayResponse> GetCustomerDisplayAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/operations/customer-display");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<CustomerDisplayResponse>(response, cancellationToken);
    }

    public async Task<List<AuditLogEntryResponse>> GetAuditLogAsync(int limit = 60, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/operations/audit?limit={limit}");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<List<AuditLogEntryResponse>>(response, cancellationToken);
    }

    public async Task<Guid> CreateMarketAsync(string eventName, decimal openingOdds, bool isActive, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/operations/markets", JsonContent.Create(new
        {
            eventName,
            openingOdds,
            isActive
        }));

        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadAsync<CreatedResponse>(response, cancellationToken);
        return payload.Id;
    }

    public async Task UpdateMarketAsync(Guid marketId, string eventName, decimal openingOdds, bool isActive, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/operations/markets/{marketId}", JsonContent.Create(new
        {
            eventName,
            openingOdds,
            isActive
        }));

        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<decimal> CreateBetAsync(Guid marketId, Guid? bettorId, string? bettorName, decimal stake, bool isCommissionFeePaid, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/operations/bets", JsonContent.Create(new
        {
            marketId,
            bettorId,
            bettorName,
            stake,
            isCommissionFeePaid
        }));

        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadAsync<AppliedOddsResponse>(response, cancellationToken);
        return payload.AppliedOdds;
    }

    public async Task<decimal> UpdateBetAsync(Guid betId, Guid marketId, Guid? bettorId, string? bettorName, decimal stake, bool isCommissionFeePaid, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/operations/bets/{betId}", JsonContent.Create(new
        {
            marketId,
            bettorId,
            bettorName,
            stake,
            isCommissionFeePaid
        }));

        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadAsync<AppliedOddsResponse>(response, cancellationToken);
        return payload.AppliedOdds;
    }

    public async Task DeleteBetAsync(Guid betId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/operations/bets/{betId}");
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task SetBetOutcomeAsync(Guid betId, BetOutcomeStatus outcomeStatus, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/operations/bets/{betId}/outcome", JsonContent.Create(new
        {
            outcomeStatus
        }));

        using var response = await SendAsync(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, $"{authOptions.ServerBaseUrl.TrimEnd('/')}{relativeUrl}");
        if (content is not null)
        {
            request.Content = content;
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                await operatorAuthService.GetAccessTokenAsync(cancellationToken));

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                using var retryRequest = await CloneRequestAsync(request, cancellationToken);
                var refreshedSession = await operatorAuthService.ForceReauthenticateAsync(cancellationToken);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    refreshedSession.AccessToken);

                response = await httpClient.SendAsync(retryRequest, cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            throw await CreateApiExceptionAsync(response, cancellationToken);
        }
        catch (ApiClientException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiClientException("Server D3Bet neodpověděl včas. Zkuste akci prosím opakovat za okamžik.", isTransient: true);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiClientException("Nepodařilo se spojit se serverem D3Bet. Zkontrolujte připojení nebo dostupnost backendu.", isTransient: true, innerException: ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ApiClientException("Relaci se nepodařilo bezpečně obnovit. Přihlaste se prosím znovu.", HttpStatusCode.Unauthorized, innerException: ex);
        }
    }

    private static async Task<ApiClientException> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = response.StatusCode;
        var detail = TryExtractMessage(payload);
        var traceId = TryExtractTraceId(payload) ?? TryReadHeader(response, "X-Correlation-ID");
        var message = statusCode switch
        {
            HttpStatusCode.BadRequest => detail ?? "Server odmítl zadaná data. Zkontrolujte prosím formulář a zkuste to znovu.",
            HttpStatusCode.Unauthorized => "Přihlášení už není platné. Obnovte prosím relaci a zkuste akci znovu.",
            HttpStatusCode.Forbidden => "Pro tuto akci nemáte oprávnění. Pokud ji potřebujete, přihlaste se administrátorským účtem.",
            HttpStatusCode.NotFound => detail ?? "Požadovaný záznam už na serveru neexistuje nebo byl mezitím změněn.",
            HttpStatusCode.Conflict => detail ?? "Server hlásí konflikt dat. Obnovte přehled a zkuste akci zopakovat nad aktuálními daty.",
            HttpStatusCode.InternalServerError => "Na serveru D3Bet došlo k chybě. Zkuste to prosím za chvíli znovu.",
            HttpStatusCode.ServiceUnavailable => "Server D3Bet je dočasně nedostupný. Vyčkejte prosím chvíli a akci opakujte.",
            _ => detail ?? $"Server vrátil neočekávanou odpověď {(int)statusCode}."
        };

        var isTransient = statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.Conflict
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            message = $"{message} Referenční kód: {traceId}.";
        }

        return new ApiClientException(message, statusCode, traceId, isTransient);
    }

    private static string? TryExtractMessage(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (TryGetString(root, "detail", out var detail))
            {
                return detail;
            }

            if (TryGetString(root, "title", out var title))
            {
                return title;
            }

            if (TryGetString(root, "message", out var message))
            {
                return message;
            }

            if (TryGetString(root, "error", out var error))
            {
                return error;
            }
        }
        catch (JsonException)
        {
        }

        return payload.Trim();
    }

    private static string? TryExtractTraceId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (TryGetString(root, "traceId", out var traceId))
            {
                return traceId;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string? TryReadHeader(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            return values.FirstOrDefault();
        }

        if (response.Content.Headers.TryGetValues(headerName, out values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Server vrátil prázdnou odpověď.");
    }

    private sealed class AppliedOddsResponse
    {
        public decimal AppliedOdds { get; set; }
    }

    private sealed class CreatedResponse
    {
        public Guid Id { get; set; }
    }

    public sealed class CustomerDisplayResponse
    {
        public DateTime GeneratedAtUtc { get; set; }

        public List<CustomerDisplayMarketResponse> Markets { get; set; } = [];
    }

    public sealed class CustomerDisplayMarketResponse
    {
        public Guid MarketId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public decimal CurrentOdds { get; set; }

        public decimal TotalStake { get; set; }

        public int TicketCount { get; set; }
    }

    public sealed class AuditLogEntryResponse
    {
        public long Id { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        public string ActorId { get; set; } = string.Empty;

        public string ActorName { get; set; } = string.Empty;

        public string ActorRoles { get; set; } = string.Empty;

        public string TraceId { get; set; } = string.Empty;

        public string? DetailJson { get; set; }
    }
}
