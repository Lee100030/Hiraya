using Hiraya.Shared.Models;
using Hiraya.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hiraya.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(HirayaLearningCenterDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migrate failed; attempting EnsureCreated.");
            await db.Database.EnsureCreatedAsync();
        }

        if (await db.Users.AnyAsync())
            return;

        logger.LogInformation("Seeding hiraya_learning_center with development data.");
        var seed = SeedData.Create();
        PermissionCatalog.EnsureRows(seed);
        seed.Roles = UserRoles.All.Select(code => new AppRole
        {
            RoleId = "role_" + code,
            Code = code,
            Name = Navigation.RoleLabel(code)
        }).ToList();
        if (string.IsNullOrWhiteSpace(seed.Settings.SettingsId))
            seed.Settings.SettingsId = "center";

        db.Users.AddRange(seed.Users);
        db.Roles.AddRange(seed.Roles);
        db.RolePermissions.AddRange(seed.RolePermissions);
        db.TeacherProfiles.AddRange(seed.TeacherProfiles);
        db.Students.AddRange(seed.Students);
        db.Programs.AddRange(seed.Programs);
        db.Classes.AddRange(seed.Classes);
        db.Enrollments.AddRange(seed.Enrollments);
        db.EnrollmentApplications.AddRange(seed.EnrollmentApplications);
        db.Attendance.AddRange(seed.Attendance);
        db.ProgressRecords.AddRange(seed.Reports);
        db.Schedules.AddRange(seed.Schedules);
        db.TeacherWorkShifts.AddRange(seed.TeacherWorkShifts);
        db.TeacherRoleOptions.AddRange(seed.TeacherRoleOptions);
        db.News.AddRange(seed.News);
        db.LeaveRequests.AddRange(seed.LeaveRequests);
        db.Payments.AddRange(seed.Payments);
        db.Notifications.AddRange(seed.Alerts);
        db.AuditLogs.AddRange(seed.AuditLogs);
        db.LearningModules.AddRange(seed.LearningModules);
        db.LearningModuleVersions.AddRange(seed.LearningModuleVersions);
        db.Settings.Add(seed.Settings);
        await db.SaveChangesAsync();
    }
}
