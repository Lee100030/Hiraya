using Hiraya.Shared.Models;

namespace Hiraya.Services;

public static class AppExperience
{
    public static bool IsNativeMobile =>
        DeviceInfo.Current.Platform == DevicePlatform.Android ||
        DeviceInfo.Current.Platform == DevicePlatform.iOS;

    public static bool IsAndroid =>
        DeviceInfo.Current.Platform == DevicePlatform.Android;

    public static bool UseMobileUserShell(HirayaUser? user) =>
        user is not null && Navigation.IsFamilyRole(user.Role);

    public static bool UseStaffOnMobileNotice(HirayaUser? user) =>
        IsNativeMobile && user is not null && !Navigation.IsFamilyRole(user.Role);

    public static bool UseTeacherWorkspace(HirayaUser? user) =>
        user?.Role == UserRoles.Teacher && !IsNativeMobile && !UseMobileUserShell(user);

    public static bool IsMobileUserAllowedPath(string relativePath)
    {
        var path = Normalize(relativePath);
        if (string.IsNullOrEmpty(path) || path is "dashboard" or "denied" or "not-found")
            return true;
        if (path.StartsWith("modules/", StringComparison.Ordinal))
            return true;

        return path is
            "children" or
            "more" or
            "schedules" or
            "calendar" or
            "attendance" or
            "progress" or
            "payments" or
            "modules" or
            "notifications" or
            "announcements" or
            "settings/profile" or
            "settings/account";
    }

    public static bool IsStaffOnMobileAllowedPath(string relativePath)
    {
        var path = Normalize(relativePath);
        return string.IsNullOrEmpty(path) || path is "dashboard" or "staff-mobile" or "denied" or "not-found";
    }

    public static bool IsTeacherAllowedPath(string relativePath)
    {
        var path = Normalize(relativePath);
        if (string.IsNullOrEmpty(path) || path is "dashboard" or "denied" or "not-found")
            return true;
        if (path.StartsWith("modules/", StringComparison.Ordinal))
            return true;

        return path is
            "classes" or
            "roster" or
            "attendance" or
            "progress" or
            "schedules" or
            "tasks" or
            "modules" or
            "calendar" or
            "notifications" or
            "announcements" or
            "settings/profile" or
            "settings/account";
    }

    private static string Normalize(string relativePath) =>
        relativePath.Split('?', '#')[0].Trim().Trim('/').ToLowerInvariant();
}
