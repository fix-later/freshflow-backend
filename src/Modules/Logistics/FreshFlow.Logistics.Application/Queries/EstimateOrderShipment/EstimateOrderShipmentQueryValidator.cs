using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.EstimateOrderShipment;

internal sealed class EstimateOrderShipmentQueryValidator
    : AbstractValidator<EstimateOrderShipmentQuery>
{
    public EstimateOrderShipmentQueryValidator() => RuleFor(x => x.OrderId).NotEmpty();
}
