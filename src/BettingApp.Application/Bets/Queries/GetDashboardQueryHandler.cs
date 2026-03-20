using BettingApp.Application.Abstractions;
using BettingApp.Application.Models;
using MediatR;

namespace BettingApp.Application.Bets.Queries;

public sealed class GetDashboardQueryHandler(IBettingRepository repository) : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var bettors = await repository.GetBettorsAsync(cancellationToken);
        var markets = await repository.GetBettingMarketsAsync(cancellationToken);
        var bets = await repository.GetRecentBetsAsync(cancellationToken);

        return new DashboardDto(bettors, markets, bets);
    }
}
