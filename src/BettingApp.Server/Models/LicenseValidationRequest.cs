namespace BettingApp.Server.Models;

public sealed record LicenseValidationRequest(
    string LicenseToken,
    string InstallationId,
    string MachineFingerprint);
