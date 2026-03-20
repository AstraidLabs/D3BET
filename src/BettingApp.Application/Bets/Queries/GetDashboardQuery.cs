using BettingApp.Application.Models;
using MediatR;

namespace BettingApp.Application.Bets.Queries;

public sealed record GetDashboardQuery : IRequest<DashboardDto>;
