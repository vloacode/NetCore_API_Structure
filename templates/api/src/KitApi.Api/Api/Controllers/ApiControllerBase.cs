using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KitApi.Application.Common.Results;

namespace KitApi.Api.Controllers;

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
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
