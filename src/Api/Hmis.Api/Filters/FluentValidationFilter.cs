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
            var entries = context.ModelState.Where(entry => entry.Value is { Errors.Count: > 0 }).ToList();

            // A malformed body also yields a generic "<parameter> field is required" entry; drop it when a
            // field-specific error explains the problem.
            var parameterNames = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (entries.Any(entry => !parameterNames.Contains(entry.Key)))
            {
                entries.RemoveAll(entry => parameterNames.Contains(entry.Key));
            }

            errors.AddRange(entries.SelectMany(entry => entry.Value!.Errors.Select(error => new ApiError(
                ErrorCodes.ValidationError,
                ToClientMessage(error.ErrorMessage),
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

    /// <summary>
    /// Model-binding messages can describe internals (CLR type names, JSON byte positions); clients get a neutral message.
    /// Input-formatter exception text is already disabled in Program.cs; this also covers converter messages.
    /// </summary>
    private static string ToClientMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) || message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Path: $", StringComparison.Ordinal)
            ? "The value is not valid."
            : message;

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
