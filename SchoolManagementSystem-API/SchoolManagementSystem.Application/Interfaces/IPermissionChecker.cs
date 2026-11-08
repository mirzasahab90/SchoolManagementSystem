namespace SchoolManagementSystem.Application.Interfaces;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(int userId, string moduleKey, string action, CancellationToken ct = default);

    void Invalidate();
}
