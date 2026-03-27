namespace BettingApp.Server.Configuration;

public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    public string SharedSecret { get; set; } = "D3BET-LICENSING-CHANGE-ME";

    public int LicenseValidityDays { get; set; } = 365;

    public List<BootstrapLicenseOption> BootstrapLicenses { get; set; } = [];
}

public sealed class BootstrapLicenseOption
{
    public string Email { get; set; } = string.Empty;

    public string ActivationKeyBase64 { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
}
