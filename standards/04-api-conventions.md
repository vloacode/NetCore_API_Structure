# Convenciones de la API

> **Aplica a:** Todos los perfiles  
> **Propósito:** Controller base y mapeo Result→HTTP, ValidationFilter, manejo global de excepciones y OpenAPI.  
> Índice general: `standards/00-INDEX.md`

## Capa API

### Controller base — `Api/Controllers/ApiControllerBase.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Application.Common.Results;

namespace {Project}.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);

    protected IActionResult HandleResult(Result result)
        => result.IsSuccess ? NoContent() : ToProblem(result.Error);

    protected IActionResult HandleCreated<T>(Result<T> result, string actionName, Func<T, object> routeValues)
        => result.IsSuccess ? CreatedAtAction(actionName, routeValues(result.Value), result.Value) : ToProblem(result.Error);

    /// <summary>Convierte un Error de negocio en ProblemDetails (RFC 9457) con el código HTTP correcto.</summary>
    protected IActionResult ToProblem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        ProblemDetails problem = error.Details is { Count: > 0 }
            ? new ValidationProblemDetails(error.Details.ToDictionary(d => d.Key, d => d.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = error.Message;
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
```

| `ErrorType` | HTTP |
|---|---|
| (éxito con valor) | 200 OK / 201 Created |
| (éxito sin valor) | 204 No Content |
| `Validation` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Failure` | 400 |
| Excepción no controlada | 500 (handler global) |

### Validación automática — `Api/Filters/ValidationFilter.cs`
```csharp
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace {Project}.Api.Filters;

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

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            var factory = services.GetRequiredService<ProblemDetailsFactory>();
            var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}
```

### Manejo global de excepciones — `Api/Errors/GlobalExceptionHandler.cs`
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace {Project}.Api.Errors;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "El registro fue modificado por otro usuario. Recargue e intente de nuevo."),
            OperationCanceledException => (499, "Solicitud cancelada por el cliente."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error interno.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Excepción no controlada en {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Excepción controlada ({Status}) en {Path}", status, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title }   // nunca exponer exception.Message al cliente
        });
    }
}
```

### OpenAPI con esquema Bearer — `Api/OpenApi/BearerSecuritySchemeTransformer.cs`
```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace {Project}.Api.OpenApi;

/// <summary>Agrega el esquema JWT Bearer al documento para probar endpoints protegidos desde Scalar.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token JWT obtenido en /api/auth/login"
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    }
}
```
> .NET 10 usa **Microsoft.OpenApi 2.x** (namespace `Microsoft.OpenApi`, sin `.Models`). Si el compilador marca diferencias de API, ajustar a la versión instalada.
