namespace BettingApp.Wpf.Services;

public sealed class OperatorSessionContext
{
    public OperatorSessionData? CurrentSession { get; private set; }

    public bool IsAuthenticated => CurrentSession is not null;

    public string DisplayName => CurrentSession?.UserName ?? "Nepřihlášený provozovatel";

    public string RolesDisplay => CurrentSession is null || CurrentSession.Roles.Count == 0
        ? "Bez role"
        : string.Join(", ", CurrentSession.Roles);

    public bool IsAdmin => HasRole("Admin");

    public bool IsOperator => HasRole("Admin") || HasRole("Operator");

    public void Set(OperatorSessionData session)
    {
        CurrentSession = session;
    }

    public void Clear()
    {
        CurrentSession = null;
    }

    public bool HasRole(string roleName)
    {
        return CurrentSession?.Roles.Any(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
