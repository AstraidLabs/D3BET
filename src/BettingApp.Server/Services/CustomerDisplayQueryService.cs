using BettingApp.Application.Bets.Queries;
using BettingApp.Server.Models;
using MediatR;

namespace BettingApp.Server.Services;

public sealed class CustomerDisplayQueryService(IMediator mediator)
{
    public async Task<CustomerDisplayResponse> GetAsync(CancellationToken cancellationToken)
    {
        var dashboard = await mediator.Send(new GetDashboardQuery(), cancellationToken);

        var markets = dashboard.Markets
            .Where(market => market.IsActive)
            .Select(market =>
            {
                var relatedBets = dashboard.RecentBets
                    .Where(bet => bet.BettingMarketId == market.Id)
                    .ToArray();

                return new CustomerDisplayMarketResponse(
                    market.Id,
                    market.EventName,
                    market.CurrentOdds,
                    relatedBets.Sum(bet => bet.Stake),
                    relatedBets.Length);
            })
            .OrderByDescending(market => market.TotalStake)
            .ThenBy(market => market.EventName)
            .ToArray();

        return new CustomerDisplayResponse(DateTime.UtcNow, markets);
    }
}
