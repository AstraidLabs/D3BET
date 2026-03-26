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

    public async Task<D3CreditWalletResponse> GetD3CreditWalletAsync(Guid bettorId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/operations/d3credit/wallets/{bettorId}");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditWalletResponse>(response, cancellationToken);
    }

    public async Task<D3CreditTopUpResponse> TopUpD3CreditAsync(Guid? bettorId, string? bettorName, decimal realMoneyAmount, string currencyCode = "CZK", CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/operations/d3credit/topups/test", JsonContent.Create(new
        {
            bettorId,
            bettorName,
            realMoneyAmount,
            currencyCode
        }));

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditTopUpResponse>(response, cancellationToken);
    }

    public async Task<D3CreditQuoteResponse> GetD3CreditQuoteAsync(Guid marketId, decimal creditStake, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/operations/d3credit/markets/{marketId}/quote", JsonContent.Create(new
        {
            creditStake
        }));

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditQuoteResponse>(response, cancellationToken);
    }

    public async Task<D3CreditBetPlacementResponse> CreateCreditBetAsync(Guid marketId, Guid? bettorId, string? bettorName, decimal creditStake, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/operations/d3credit/markets/{marketId}/bets", JsonContent.Create(new
        {
            bettorId,
            bettorName,
            creditStake
        }));

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditBetPlacementResponse>(response, cancellationToken);
    }

    public async Task<D3CreditBetPlacementResponse> UpdateCreditBetAsync(Guid betId, Guid marketId, Guid? bettorId, string? bettorName, decimal creditStake, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/operations/d3credit/bets/{betId}?marketId={marketId}", JsonContent.Create(new
        {
            bettorId,
            bettorName,
            creditStake
        }));

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditBetPlacementResponse>(response, cancellationToken);
    }

    public async Task<D3CreditAdminSettingsResponse> GetD3CreditAdminSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/operations/d3credit/admin/settings");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditAdminSettingsResponse>(response, cancellationToken);
    }

    public async Task<D3CreditAdminSettingsResponse> SaveD3CreditAdminSettingsAsync(D3CreditAdminSettingsResponse settings, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, "/api/operations/d3credit/admin/settings", JsonContent.Create(settings));
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditAdminSettingsResponse>(response, cancellationToken);
    }

    public async Task<List<D3CreditAdminWalletListItemResponse>> GetD3CreditAdminWalletsAsync(string? search = null, int limit = 80, CancellationToken cancellationToken = default)
    {
        var encodedSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}";
        using var request = CreateRequest(HttpMethod.Get, $"/api/operations/d3credit/admin/wallets?limit={limit}{encodedSearch}");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<List<D3CreditAdminWalletListItemResponse>>(response, cancellationToken);
    }

    public async Task<List<D3CreditAdminTransactionResponse>> GetD3CreditAdminTransactionsAsync(string? search = null, int limit = 120, CancellationToken cancellationToken = default)
    {
        var encodedSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}";
        using var request = CreateRequest(HttpMethod.Get, $"/api/operations/d3credit/admin/transactions?limit={limit}{encodedSearch}");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<List<D3CreditAdminTransactionResponse>>(response, cancellationToken);
    }

    public async Task<D3CreditWalletResponse> ApplyD3CreditManualAdjustmentAsync(Guid? bettorId, string? bettorName, decimal creditAmount, decimal? realMoneyAmount, string? currencyCode, string reason, string? reference, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/operations/d3credit/admin/adjustments", JsonContent.Create(new
        {
            bettorId,
            bettorName,
            creditAmount,
            realMoneyAmount,
            currencyCode,
            reason,
            reference
        }));

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditWalletResponse>(response, cancellationToken);
    }

    public async Task<D3CreditWalletResponse> RefundD3CreditBetAsync(Guid betId, string reason, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/operations/d3credit/admin/bets/{betId}/refund", JsonContent.Create(new
        {
            reason
        }));

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<D3CreditWalletResponse>(response, cancellationToken);
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
                var refreshedSession = await operatorAuthService.TryRefreshSessionSilentlyAsync(cancellationToken);
                if (refreshedSession is not null)
                {
                    using var retryRequest = await CloneRequestAsync(request, cancellationToken);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue(
                        "Bearer",
                        refreshedSession.AccessToken);

                    response = await httpClient.SendAsync(retryRequest, cancellationToken);
                }
                else
                {
                    throw new ApiClientException(
                        "Přihlášení už není platné. Obnovte prosím relaci a zkuste akci znovu.",
                        HttpStatusCode.Unauthorized);
                }
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

    public sealed class D3CreditWalletResponse
    {
        public Guid BettorId { get; set; }

        public string BettorName { get; set; } = string.Empty;

        public string CreditCode { get; set; } = "D3Kredit";

        public decimal Balance { get; set; }

        public decimal LastMoneyToCreditRate { get; set; }

        public decimal LastCreditToMoneyRate { get; set; }

        public List<D3CreditTransactionResponse> Transactions { get; set; } = [];
    }

    public sealed class D3CreditTransactionResponse
    {
        public Guid Id { get; set; }

        public D3CreditTransactionType Type { get; set; }

        public decimal CreditAmount { get; set; }

        public decimal RealMoneyAmount { get; set; }

        public string RealCurrencyCode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class D3CreditTopUpResponse
    {
        public string PaymentGateway { get; set; } = string.Empty;

        public string PaymentReference { get; set; } = string.Empty;

        public string CreditCode { get; set; } = string.Empty;

        public decimal NewBalance { get; set; }

        public decimal AddedCredits { get; set; }

        public decimal RealMoneyAmount { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;

        public decimal MoneyToCreditRate { get; set; }
    }

    public sealed class D3CreditQuoteResponse
    {
        public Guid MarketId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public string CreditCode { get; set; } = "D3Kredit";

        public string RealCurrencyCode { get; set; } = "CZK";

        public decimal MoneyToCreditRate { get; set; }

        public decimal CreditToMoneyRate { get; set; }

        public decimal MarketParticipationMultiplier { get; set; }

        public decimal RealMoneyAmount { get; set; }

        public decimal CreditAmount { get; set; }

        public decimal PotentialPayoutCredits { get; set; }

        public decimal PotentialPayoutRealMoney { get; set; }
    }

    public sealed class D3CreditBetPlacementResponse
    {
        public decimal AppliedOdds { get; set; }

        public D3CreditWalletResponse Wallet { get; set; } = new();

        public D3CreditQuoteResponse Quote { get; set; } = new();
    }

    public sealed class D3CreditAdminSettingsResponse
    {
        public string CreditCode { get; set; } = "D3Kredit";

        public string BaseCurrencyCode { get; set; } = "CZK";

        public decimal BaseCreditsPerCurrencyUnit { get; set; }

        public decimal BaseCurrencyUnitsPerCredit { get; set; }

        public int LowParticipationThreshold { get; set; }

        public decimal LowParticipationBoostPercent { get; set; }

        public int HighParticipationThreshold { get; set; }

        public decimal HighParticipationReductionPercent { get; set; }

        public decimal TotalStakePressureDivisor { get; set; }

        public decimal MaxPressureReductionPercent { get; set; }

        public decimal OddsVolatilityWeightPercent { get; set; }

        public bool EnableTestTopUpGateway { get; set; }

        public bool EnableManualCreditAdjustments { get; set; }

        public bool EnableManualBetRefunds { get; set; }

        public decimal DefaultTopUpAmount { get; set; }

        public List<D3CreditMarketAdminRuleResponse> MarketRules { get; set; } = [];
    }

    public sealed class D3CreditMarketAdminRuleResponse
    {
        public Guid MarketId { get; set; }

        public bool IsEnabled { get; set; }

        public decimal AdditionalMultiplierPercent { get; set; }

        public decimal? OverrideMoneyToCreditRate { get; set; }

        public decimal? OverrideCreditToMoneyRate { get; set; }

        public string? Note { get; set; }
    }

    public sealed class D3CreditAdminWalletListItemResponse
    {
        public Guid BettorId { get; set; }

        public string BettorName { get; set; } = string.Empty;

        public string CreditCode { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public decimal LastMoneyToCreditRate { get; set; }

        public decimal LastCreditToMoneyRate { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class D3CreditAdminTransactionResponse
    {
        public Guid Id { get; set; }

        public Guid BettorId { get; set; }

        public string BettorName { get; set; } = string.Empty;

        public D3CreditTransactionType Type { get; set; }

        public decimal CreditAmount { get; set; }

        public decimal RealMoneyAmount { get; set; }

        public string RealCurrencyCode { get; set; } = string.Empty;

        public decimal MoneyToCreditRate { get; set; }

        public decimal CreditToMoneyRate { get; set; }

        public decimal MarketParticipationMultiplier { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}
