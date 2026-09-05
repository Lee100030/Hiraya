using Hiraya.Shared.Models;
using Hiraya.Shared.Services;

namespace Hiraya.Web.Services;

public sealed class WebSessionService
{
    public HirayaUser? CurrentUser { get; private set; }

    public string? SelectedStudentId { get; private set; }

    public bool IsLoggedIn => CurrentUser is not null;

    public event Action? Changed;

    public static string PortalPath(HirayaUser user)
    {
        if (Navigation.IsFamilyRole(user.Role))
            return "/portal/family";
        if (user.Role == UserRoles.Teacher)
            return "/portal/teacher";
        return "/portal/staff";
    }

    public void SignIn(HirayaUser user)
    {
        CurrentUser = AccountService.ClonePublic(user);
        SelectedStudentId = null;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        CurrentUser = null;
        SelectedStudentId = null;
        Changed?.Invoke();
    }

    public void SelectStudent(string? studentId)
    {
        SelectedStudentId = string.IsNullOrWhiteSpace(studentId) ? null : studentId;
        Changed?.Invoke();
    }
}
