namespace BettingApp.Application.Services;

public static class DynamicOddsCalculator
{
    private const decimal ReductionPerExistingBet = 0.03m;
    private const decimal ReductionPerUniqueBettor = 0.04m;
    private const decimal ReductionPerStakeBlock = 0.02m;
    private const decimal StakeBlockSize = 1000m;
    private const decimal MinimumOdds = 1.01m;

    public static decimal CalculateAdjustedOdds(decimal requestedOdds, int existingBetsCount, int uniqueBettorCount, decimal totalStake)
    {
        var stakeReductionBlocks = totalStake <= 0m
            ? 0m
            : totalStake / StakeBlockSize;

        var adjustedOdds = requestedOdds
            - (existingBetsCount * ReductionPerExistingBet)
            - (uniqueBettorCount * ReductionPerUniqueBettor)
            - (stakeReductionBlocks * ReductionPerStakeBlock);

        return Math.Max(MinimumOdds, Math.Round(adjustedOdds, 2, MidpointRounding.AwayFromZero));
    }
}
