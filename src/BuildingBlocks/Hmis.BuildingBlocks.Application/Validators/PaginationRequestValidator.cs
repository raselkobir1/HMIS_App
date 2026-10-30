using FluentValidation;
using Hmis.BuildingBlocks.Application.Pagination;

namespace Hmis.BuildingBlocks.Application.Validators;

/// <summary>Base validator; module list-query validators inherit it to get paging rules.</summary>
public class PaginationRequestValidator<T> : AbstractValidator<T> where T : PaginationRequest
{
    public PaginationRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PaginationRequest.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy).MaximumLength(50);
        RuleFor(x => x.SortDirection).IsInEnum();
    }
}

public sealed class PaginationRequestValidator : PaginationRequestValidator<PaginationRequest>;
