using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace BettingApp.Wpf.Services;

public static class PreLoginErrorTranslator
{
    public static string Translate(Exception exception, string fallbackMessage)
    {
        if (exception is ApiClientException apiClientException)
        {
            return string.IsNullOrWhiteSpace(apiClientException.Message) ? fallbackMessage : apiClientException.Message;
        }

        if (exception is HttpRequestException httpRequestException)
        {
            if (httpRequestException.StatusCode == HttpStatusCode.Unauthorized)
            {
                return "Přístup k serveru byl odmítnut. Obnovte licenci nebo přihlášení a zkuste to znovu.";
            }

            return "Nepodařilo se spojit se serverem D3Bet. Zkontrolujte prosím, že backend běží a je dostupný na síti.";
        }

        if (exception is OperationCanceledException)
        {
            return "Spojení se serverem trvalo příliš dlouho. Zkuste to prosím znovu za okamžik.";
        }

        if (exception is InvalidOperationException invalidOperationException)
        {
            return string.IsNullOrWhiteSpace(invalidOperationException.Message) ? fallbackMessage : invalidOperationException.Message;
        }

        return string.IsNullOrWhiteSpace(exception.Message) ? fallbackMessage : exception.Message;
    }

    public static async Task EnsureSuccessOrThrowFriendlyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        Func<string, HttpStatusCode, string>? mapper = null)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryExtractDetail(payload);
        var friendly = mapper?.Invoke(detail ?? string.Empty, response.StatusCode)
            ?? BuildDefaultMessage(detail, response.StatusCode);

        throw new InvalidOperationException(friendly);
    }

    private static string BuildDefaultMessage(string? detail, HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Server odmítl požadavek ještě před přihlášením. Zkontrolujte prosím zadané údaje a stav licence.",
            HttpStatusCode.Unauthorized => "Server odmítl přístup. Ověřte licenci nebo přihlášení a zkuste to znovu.",
            HttpStatusCode.Forbidden => "Tahle akce není pro aktuální klientský stav povolená.",
            HttpStatusCode.NotFound => "Požadovaný endpoint na serveru nebyl nalezen.",
            _ => $"Server vrátil neočekávanou odpověď {(int)statusCode}."
        };
    }

    private static string? TryExtractDetail(string? payload)
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

            if (TryGetString(root, "error_description", out var errorDescription))
            {
                return errorDescription;
            }

            if (TryGetString(root, "title", out var title))
            {
                return title;
            }
        }
        catch (JsonException)
        {
        }

        return payload.Trim();
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
