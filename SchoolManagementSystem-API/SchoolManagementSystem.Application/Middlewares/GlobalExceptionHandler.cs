using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Middlewares;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is CampusAccessException campusError)
        {
            logger.LogWarning("Campus access denied: {Message}", campusError.Message);
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(
                new ResponseDto<object>(null, 403, campusError.Message),
                cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ResponseDto<object>(null, 500, "An unexpected error occurred."),
            cancellationToken);
        return true;
    }
}
