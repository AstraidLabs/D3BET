using System.Text.Json.Serialization;

namespace BettingApp.Server.Models;

public sealed class LicenseConfirmationRequest
{
    [JsonPropertyName("licenseToken")]
    public string LicenseToken { get; set; } = string.Empty;

    [JsonPropertyName("installationId")]
    public string InstallationId { get; set; } = string.Empty;

    [JsonPropertyName("machineFingerprint")]
    public string MachineFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("confirmationCode")]
    public string ConfirmationCode { get; set; } = string.Empty;

    [JsonPropertyName("challengeSignature")]
    public string ChallengeSignature { get; set; } = string.Empty;
}
