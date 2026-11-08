namespace SchoolManagementSystem.Domain.Common;

/// <summary>A business error with the HTTP status the API returns for it.</summary>
public record Error(string Message, int Status = 400)
{
    public static Error Validation(string message) => new(message, 400);

    public static Error NotFound(string what) => new($"{what} not found.", 404);

    public static Error Conflict(string message) => new(message, 409);

    public static Error Unauthorized(string message) => new(message, 401);

    public static Error Forbidden(string message = "You do not have permission for this action.") => new(message, 403);
}
