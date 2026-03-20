namespace BettingApp.Domain.Entities;

public sealed class Bettor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
}
