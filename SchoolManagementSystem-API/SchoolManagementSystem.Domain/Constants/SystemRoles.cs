namespace SchoolManagementSystem.Domain.Constants;

public static class SystemRoles
{
    /// <summary>Full access to every module and every campus; cannot be deleted or renamed.</summary>
    public const string SuperAdmin = "SuperAdmin";

    public const string Principal = "Principal";
    public const string Accountant = "Accountant";
    public const string Teacher = "Teacher";
    public const string Clerk = "Clerk";
    public const string Parent = "Parent";
    public const string Student = "Student";
}
