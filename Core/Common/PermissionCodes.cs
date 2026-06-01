namespace Core.Common;

public static class PermissionCodes
{
    // User permissions
    public const string UsersCreate = "Users.Create";
    public const string UsersView = "Users.View";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDelete = "Users.Delete";

    // Role permissions
    public const string RolesManage = "Roles.Manage";
    public const string RolesView = "Roles.View";

    // Logs permissions
    public const string LogsView = "Logs.View";
}
