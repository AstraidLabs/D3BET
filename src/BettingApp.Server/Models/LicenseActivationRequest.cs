namespace BettingApp.Server.Models;

public sealed record LicenseActivationRequest(
    string Email,
    string ActivationKeyBase64,
    string InstallationId,
    string MachineFingerprint);
