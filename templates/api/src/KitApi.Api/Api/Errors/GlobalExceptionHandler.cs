using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitApi.Api.Errors;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency.Conflict", "El registro fue modificado por otro usuario. Recargue e intente de nuevo."),
            OperationCanceledException => (499, "Request.Cancelled", "Solicitud cancelada por el cliente."),
            BadHttpRequestException bad => (bad.StatusCode, "Request.Invalid", "La solicitud no es válida."),
            _ => (StatusCodes.Status500InternalServerError, "Server.Error", "Ocurrió un error interno.")
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
            // Nunca exponer exception.Message al cliente. traceId lo agrega CustomizeProblemDetails (standards/01).
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Extensions = { ["code"] = code } }
        });
    }
}
