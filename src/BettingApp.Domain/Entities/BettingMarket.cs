namespace BettingApp.Domain.Entities;

public sealed class BettingMarket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EventName { get; set; } = string.Empty;

    public decimal OpeningOdds { get; set; }

    public decimal CurrentOdds { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
}
