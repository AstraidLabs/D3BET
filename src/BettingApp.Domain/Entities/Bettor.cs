namespace BettingApp.Domain.Entities;

public sealed class Bettor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public ICollection<Bet> Bets { get; set; } = new List<Bet>();

    public BettorWallet? Wallet { get; set; }

    public ICollection<D3CreditTransaction> CreditTransactions { get; set; } = new List<D3CreditTransaction>();
}
