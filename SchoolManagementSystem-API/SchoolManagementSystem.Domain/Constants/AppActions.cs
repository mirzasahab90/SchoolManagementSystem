namespace SchoolManagementSystem.Domain.Constants;

public static class AppActions
{
    public const string View = "View";
    public const string Add = "Add";
    public const string Edit = "Edit";
    public const string Delete = "Delete";
    public const string Approve = "Approve";
    public const string Export = "Export";

    public static readonly string[] All = [View, Add, Edit, Delete, Approve, Export];
}
