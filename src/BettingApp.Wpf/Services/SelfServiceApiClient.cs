using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BettingApp.Wpf.Services;

public sealed class SelfServiceApiClient(OperatorAuthOptions authOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public Task<SelfServiceActionResponse> RegisterAsync(
        string userName,
        string email,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken = default) =>
        SendAndReadAsync<SelfServiceActionResponse>(
            HttpMethod.Post,
            "/api/account/register",
            JsonContent.Create(new
            {
                userName,
                email,
                password,
                confirmPassword
            }),
            cancellationToken);

    public Task<SelfServiceActionResponse> ActivateAsync(
        string userNameOrEmail,
        string token,
        CancellationToken cancellationToken = default) =>
        SendAndReadAsync<SelfServiceActionResponse>(
            HttpMethod.Post,
            "/api/account/activate",
            JsonContent.Create(new
            {
                userNameOrEmail,
                token
            }),
            cancellationToken);

    public Task<SelfServiceActionResponse> ReactivateAsync(
        string userNameOrEmail,
        CancellationToken cancellationToken = default) =>
        SendAndReadAsync<SelfServiceActionResponse>(
            HttpMethod.Post,
            "/api/account/reactivate",
            JsonContent.Create(new
            {
                userNameOrEmail
            }),
            cancellationToken);

    public Task<SelfServiceActionResponse> ForgotPasswordAsync(
        string userNameOrEmail,
        CancellationToken cancellationToken = default) =>
        SendAndReadAsync<SelfServiceActionResponse>(
            HttpMethod.Post,
            "/api/account/forgot-password",
            JsonContent.Create(new
            {
                userNameOrEmail
            }),
            cancellationToken);

    public Task<SelfServiceActionResponse> ResetPasswordAsync(
        string userNameOrEmail,
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default) =>
        SendAndReadAsync<SelfServiceActionResponse>(
            HttpMethod.Post,
            "/api/account/reset-password",
            JsonContent.Create(new
            {
                userNameOrEmail,
                token,
                newPassword,
                confirmPassword
            }),
            cancellationToken);

    public Task<AccountProfileResponse> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default) =>
        SendAndReadAsync<AccountProfileResponse>(
            HttpMethod.Get,
            "/api/account/profile",
            null,
            cancellationToken,
            accessToken);

    public Task<UpdateProfileResponse> UpdateProfileAsync(
        string accessToken,
        string userName,
        string email,
        CancellationToken cancellationToken = default) =>
        SendAndReadAsync<UpdateProfileResponse>(
            HttpMethod.Put,
            "/api/account/profile",
            JsonContent.Create(new
            {
                userName,
                email
            }),
            cancellationToken,
            accessToken);

    private async Task<T> SendAndReadAsync<T>(
        HttpMethod method,
        string relativeUrl,
        HttpContent? content,
        CancellationToken cancellationToken,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, $"{authOptions.ServerBaseUrl.TrimEnd('/')}{relativeUrl}");
        if (content is not null)
        {
            request.Content = content;
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
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
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Server vrátil prázdnou odpověď.");
    }

    private static async Task<ApiClientException> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryExtractMessage(payload);
        var traceId = TryExtractTraceId(payload);
        var statusCode = response.StatusCode;

        var message = statusCode switch
        {
            HttpStatusCode.BadRequest => detail ?? "Server odmítl zadaná data. Zkontrolujte prosím formulář a zkuste to znovu.",
            HttpStatusCode.Unauthorized => detail ?? "Přihlášení už není platné. Obnovte prosím relaci a zkuste akci znovu.",
            HttpStatusCode.Forbidden => detail ?? "Pro tuto akci nemáte oprávnění.",
            HttpStatusCode.NotFound => detail ?? "Požadovaný účet nebo zdroj nebyl nalezen.",
            HttpStatusCode.Conflict => detail ?? "Server hlásí konflikt dat. Obnovte prosím stav a zkuste akci znovu.",
            _ => detail ?? $"Server vrátil neočekávanou odpověď {(int)statusCode}."
        };

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            message = $"{message} Referenční kód: {traceId}.";
        }

        return new ApiClientException(message, statusCode, traceId);
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

            if (root.TryGetProperty("traceId", out var traceProperty) && traceProperty.ValueKind == JsonValueKind.String)
            {
                return traceProperty.GetString();
            }

            if (root.TryGetProperty("extensions", out var extensions)
                && extensions.ValueKind == JsonValueKind.Object
                && extensions.TryGetProperty("traceId", out traceProperty)
                && traceProperty.ValueKind == JsonValueKind.String)
            {
                return traceProperty.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }
}

public sealed class SelfServiceActionResponse
{
    public string Message { get; set; } = string.Empty;

    public SelfServicePreviewResponse? Preview { get; set; }
}

public sealed class SelfServicePreviewResponse
{
    public string Purpose { get; set; } = string.Empty;

    public string UserNameOrEmail { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string? Link { get; set; }
}

public sealed class AccountProfileResponse
{
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool EmailConfirmed { get; set; }

    public List<string> Roles { get; set; } = [];
}

public sealed class UpdateProfileResponse
{
    public string Message { get; set; } = string.Empty;

    public SelfServicePreviewResponse? Preview { get; set; }

    public AccountProfileResponse Profile { get; set; } = new();
}
