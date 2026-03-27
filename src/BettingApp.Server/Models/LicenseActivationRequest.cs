using System.Text.Json.Serialization;

namespace BettingApp.Server.Models;

public sealed class LicenseActivationRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("activationKeyBase64")]
    public string ActivationKeyBase64 { get; set; } = string.Empty;

    [JsonPropertyName("installationId")]
    public string InstallationId { get; set; } = string.Empty;

    [JsonPropertyName("machineFingerprint")]
    public string MachineFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("clientPublicKey")]
    public string ClientPublicKey { get; set; } = string.Empty;
}
