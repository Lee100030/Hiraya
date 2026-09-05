namespace Hiraya.Shared.Models;

public static class PermissionKeys
{
    public const string ViewStudents = "students.view";
    public const string CreateStudents = "students.create";
    public const string EditStudents = "students.edit";
    public const string ArchiveStudents = "students.archive";
    public const string ViewTeachers = "teachers.view";
    public const string ManageTeachers = "teachers.manage";
    public const string ManageClasses = "classes.manage";
    public const string ManageEnrollment = "enrollment.manage";
    public const string ManageAttendance = "attendance.manage";
    public const string ManageProgress = "progress.manage";
    public const string ManagePayments = "payments.manage";
    public const string ManageReports = "reports.view";
    public const string ManageUsers = "users.manage";
    public const string ManageSettings = "settings.manage";
    public const string ViewAudit = "audit.view";
    public const string ManageRoles = "roles.manage";
    public const string ManageModules = "modules.manage";
    public const string ViewModules = "modules.view";

    public static readonly (string Key, string Label)[] All =
    [
        (ViewStudents, "View students"),
        (CreateStudents, "Create students"),
        (EditStudents, "Edit students"),
        (ArchiveStudents, "Archive students"),
        (ViewTeachers, "View teachers / staff"),
        (ManageTeachers, "Manage teachers / staff"),
        (ManageClasses, "Manage classes"),
        (ManageEnrollment, "Manage enrollment"),
        (ManageAttendance, "Manage attendance"),
        (ManageProgress, "Manage progress"),
        (ManagePayments, "Manage payments"),
        (ManageReports, "View reports"),
        (ManageUsers, "Manage users"),
        (ManageSettings, "Manage system settings"),
        (ViewAudit, "View audit logs"),
        (ManageRoles, "Manage roles and permissions"),
        (ManageModules, "Manage learning modules"),
        (ViewModules, "View learning modules")
    ];
}

public static class PermissionCatalog
{
    public static bool Allows(HirayaDatabase? db, string? role, string permission)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        if (role == UserRoles.SuperAdmin)
            return true;

        var overrideRow = db?.RolePermissions.FirstOrDefault(p =>
            p.Role == role && p.Permission == permission);
        if (overrideRow != null)
            return overrideRow.Allowed;

        return DefaultAllows(role, permission);
    }

    public static bool DefaultAllows(string role, string permission) => role switch
    {
        UserRoles.SuperAdmin => true,
        UserRoles.Admin => permission != PermissionKeys.ManageRoles,
        UserRoles.Teacher => permission is
            PermissionKeys.ViewStudents or
            PermissionKeys.ViewTeachers or
            PermissionKeys.ManageAttendance or
            PermissionKeys.ManageProgress or
            PermissionKeys.ManageReports or
            PermissionKeys.ViewModules,
        UserRoles.Staff => permission is
            PermissionKeys.ViewStudents or
            PermissionKeys.ViewTeachers or
            PermissionKeys.ManageAttendance or
            PermissionKeys.ManagePayments or
            PermissionKeys.ManageReports,
        UserRoles.Parent => permission is PermissionKeys.ViewStudents or PermissionKeys.ManageReports or PermissionKeys.ViewModules,
        UserRoles.Student => permission is PermissionKeys.ViewStudents or PermissionKeys.ManageReports or PermissionKeys.ViewModules,
        _ => false
    };

    public static void EnsureRows(HirayaDatabase db)
    {
        foreach (var role in UserRoles.All)
        {
            foreach (var (key, _) in PermissionKeys.All)
            {
                if (db.RolePermissions.Any(p => p.Role == role && p.Permission == key))
                    continue;
                db.RolePermissions.Add(new RolePermission
                {
                    PermissionId = $"perm_{Guid.NewGuid():N}"[..18],
                    Role = role,
                    Permission = key,
                    Allowed = DefaultAllows(role, key)
                });
            }
        }
    }
}
