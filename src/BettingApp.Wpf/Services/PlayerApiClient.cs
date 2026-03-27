using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BettingApp.Domain.Entities;

namespace BettingApp.Wpf.Services;

public sealed class PlayerApiClient(
    OperatorAuthOptions authOptions,
    OperatorAuthService operatorAuthService)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<PlayerDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/player/dashboard");
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<PlayerDashboardResponse>(response, cancellationToken);
    }

    public async Task<OperationsApiClient.D3CreditTopUpResponse> TopUpAsync(decimal realMoneyAmount, string currencyCode = "CZK", CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/player/topups/test", JsonContent.Create(new
        {
            realMoneyAmount,
            currencyCode
        }));
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<OperationsApiClient.D3CreditTopUpResponse>(response, cancellationToken);
    }

    public async Task<OperationsApiClient.D3CreditQuoteResponse> GetQuoteAsync(Guid marketId, decimal creditStake, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/player/markets/{marketId}/quote", JsonContent.Create(new
        {
            creditStake
        }));
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<OperationsApiClient.D3CreditQuoteResponse>(response, cancellationToken);
    }

    public async Task<OperationsApiClient.D3CreditBetPlacementResponse> PlaceBetAsync(Guid marketId, decimal creditStake, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/player/markets/{marketId}/bets", JsonContent.Create(new
        {
            creditStake
        }));
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<OperationsApiClient.D3CreditBetPlacementResponse>(response, cancellationToken);
    }

    public async Task<CreditWithdrawalResponse> RequestWithdrawalAsync(decimal creditAmount, string currencyCode = "CZK", string? reason = null, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/player/withdrawals", JsonContent.Create(new
        {
            creditAmount,
            currencyCode,
            reason
        }));
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<CreditWithdrawalResponse>(response, cancellationToken);
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await operatorAuthService.GetAccessTokenAsync(cancellationToken));
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            var refreshedSession = await operatorAuthService.TryRefreshSessionSilentlyAsync(cancellationToken);
            if (refreshedSession is not null)
            {
                using var retryRequest = await CloneRequestAsync(request, cancellationToken);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedSession.AccessToken);
                response = await httpClient.SendAsync(retryRequest, cancellationToken);
            }
            else
            {
                throw new ApiClientException("Přihlášení už není platné. Obnovte prosím relaci a zkuste akci znovu.", System.Net.HttpStatusCode.Unauthorized);
            }
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        throw await CreateApiExceptionAsync(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Server vrátil prázdnou odpověď.");
    }

    private static async Task<ApiClientException> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ApiClientException(string.IsNullOrWhiteSpace(payload) ? $"Server vrátil chybu {(int)response.StatusCode}." : payload, response.StatusCode);
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

    public sealed class PlayerDashboardResponse
    {
        public AccountProfileResponse Profile { get; set; } = new();

        public OperationsApiClient.D3CreditWalletResponse Wallet { get; set; } = new();

        public List<PlayerMarketSummaryResponse> Markets { get; set; } = [];

        public List<PlayerBetSummaryResponse> RecentBets { get; set; } = [];

        public List<CreditWithdrawalResponse> RecentWithdrawals { get; set; } = [];

        public List<OperationsApiClient.ElectronicReceiptResponse> RecentReceipts { get; set; } = [];
    }

    public sealed class PlayerMarketSummaryResponse
    {
        public Guid MarketId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public decimal CurrentOdds { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class PlayerBetSummaryResponse
    {
        public Guid BetId { get; set; }

        public Guid? MarketId { get; set; }

        public string EventName { get; set; } = string.Empty;

        public decimal Odds { get; set; }

        public decimal Stake { get; set; }

        public string StakeCurrencyCode { get; set; } = string.Empty;

        public decimal StakeRealMoneyEquivalent { get; set; }

        public decimal PotentialPayout { get; set; }

        public BetOutcomeStatus OutcomeStatus { get; set; }

        public DateTime PlacedAtUtc { get; set; }
    }

    public sealed class CreditWithdrawalResponse
    {
        public Guid Id { get; set; }

        public Guid BettorId { get; set; }

        public decimal CreditAmount { get; set; }

        public decimal RealMoneyAmount { get; set; }

        public string RealCurrencyCode { get; set; } = string.Empty;

        public decimal CreditToMoneyRateApplied { get; set; }

        public CreditWithdrawalRequestStatus Status { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string? ProcessedReason { get; set; }

        public bool IsAutoProcessed { get; set; }

        public DateTime RequestedAtUtc { get; set; }

        public DateTime? ProcessedAtUtc { get; set; }

        public OperationsApiClient.ElectronicReceiptResponse? IssuedReceipt { get; set; }
    }
}
