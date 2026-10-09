using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace KitApi.Api.Filters;

/// <summary>Ejecuta el IValidator&lt;T&gt; registrado para cada argumento de la acción; si falla responde 400.</summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            // Claves en camelCase para que coincidan con los nombres del JSON (standards/15).
            foreach (var error in result.Errors)
                context.ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName), error.ErrorMessage);

            var factory = services.GetRequiredService<ProblemDetailsFactory>();
            var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
            problem.Title = "Uno o más campos no son válidos.";
            problem.Extensions["code"] = "Validation.Failed";
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}
