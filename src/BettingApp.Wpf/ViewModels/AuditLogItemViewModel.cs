namespace BettingApp.Wpf.ViewModels;

public sealed class AuditLogItemViewModel
{
    public long Id { get; set; }

    public string TimestampDisplay { get; set; } = string.Empty;

    public string ActionDisplay { get; set; } = string.Empty;

    public string EntityDisplay { get; set; } = string.Empty;

    public string ActorDisplay { get; set; } = string.Empty;

    public string TraceIdDisplay { get; set; } = string.Empty;

    public string DetailDisplay { get; set; } = string.Empty;
}
