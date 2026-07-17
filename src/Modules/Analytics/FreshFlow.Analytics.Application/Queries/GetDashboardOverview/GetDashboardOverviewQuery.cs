using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetDashboardOverview;

public sealed record GetDashboardOverviewQuery(DateOnly? Date = null) : IQuery<DashboardOverviewDto>;

