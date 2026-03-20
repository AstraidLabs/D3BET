namespace BettingApp.Server.Configuration;

public static class ServerStoragePaths
{
    public static string GetBettingDatabasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BettingApp",
            "betting-app.db");
    }

    public static string GetAuthDatabasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BettingApp",
            "server-auth.db");
    }
}
