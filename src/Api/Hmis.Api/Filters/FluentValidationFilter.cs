using FluentValidation;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hmis.Api.Filters;

/// <summary>
/// Explicit validation pipeline (no MediatR): converts model-binding errors, then runs the
/// FluentValidation validator registered for each action argument with ValidateAsync.
/// On failure returns 400 and the action (and therefore the manager) is never executed.
/// </summary>
public sealed class FluentValidationFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new List<ApiError>();

        if (!context.ModelState.IsValid)
        {
            errors.AddRange(context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiError(
                    ErrorCodes.ValidationError,
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage,
                    ToCamelCase(entry.Key)))));
        }
        else
        {
            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument is null)
                {
                    continue;
                }

                var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
                if (serviceProvider.GetService(validatorType) is not IValidator validator)
                {
                    continue;
                }

                var result = await validator.ValidateAsync(
                    new ValidationContext<object>(argument),
                    context.HttpContext.RequestAborted);

                errors.AddRange(result.Errors.Select(failure => new ApiError(
                    ErrorCodes.ValidationError,
                    failure.ErrorMessage,
                    ToCamelCase(failure.PropertyName))));
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Fail(
                "One or more validation errors occurred.",
                errors,
                TraceIdResolver.Resolve(context.HttpContext)));
            return;
        }

        await next();
    }

    private static string? ToCamelCase(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return string.Join('.', path.TrimStart('$', '.').Split('.')
            .Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
    }
}
