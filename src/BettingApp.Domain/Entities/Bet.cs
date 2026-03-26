namespace BettingApp.Domain.Entities;

public sealed class Bet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EventName { get; set; } = string.Empty;

    public decimal Odds { get; set; }

    public decimal Stake { get; set; }

    public string StakeCurrencyCode { get; set; } = "CZK";

    public decimal StakeRealMoneyEquivalent { get; set; }

    public decimal CreditToMoneyRateApplied { get; set; } = 1m;

    public decimal MarketParticipationMultiplierApplied { get; set; } = 1m;

    public bool IsWinning { get; set; }

    public BetOutcomeStatus OutcomeStatus { get; set; } = BetOutcomeStatus.Pending;

    public bool IsCommissionFeePaid { get; set; }

    public Guid? BettingMarketId { get; set; }

    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid BettorId { get; set; }

    public Bettor? Bettor { get; set; }

    public BettingMarket? BettingMarket { get; set; }

    public decimal PotentialPayout => Math.Round(Odds * Stake, 2, MidpointRounding.AwayFromZero);
}
