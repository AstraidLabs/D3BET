namespace BettingApp.Server.Configuration;

public sealed class BootstrapIdentityOptions
{
    public const string SectionName = "Identity:Bootstrap";

    public BootstrapUserOptions Admin { get; set; } = new()
    {
        UserName = "admin",
        Password = "Admin1234"
    };

    public BootstrapUserOptions Operator { get; set; } = new()
    {
        UserName = "operator",
        Password = "Operator1234"
    };
}

public sealed class BootstrapUserOptions
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
