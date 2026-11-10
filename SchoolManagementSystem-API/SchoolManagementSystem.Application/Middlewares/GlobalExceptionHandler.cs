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

        // Two requests raced past a uniqueness check (same admission number, roll number, CNIC, …).
        if (exception is Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } })
        {
            logger.LogWarning(exception, "Unique constraint conflict");
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(
                new ResponseDto<object>(null, 409, "This conflicts with a record that was just saved (for example the same number). Refresh and try again."),
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
