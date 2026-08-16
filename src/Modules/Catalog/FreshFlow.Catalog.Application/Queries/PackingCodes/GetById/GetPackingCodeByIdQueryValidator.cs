using FluentValidation;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.GetById;

internal sealed class GetPackingCodeByIdQueryValidator
    : AbstractValidator<GetPackingCodeByIdQuery>
{
    public GetPackingCodeByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}
