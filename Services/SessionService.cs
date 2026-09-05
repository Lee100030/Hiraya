using Hiraya.Shared.Models;
using Hiraya.Shared.Services;

namespace Hiraya.Services;

public class SessionService
{
    private const string UserIdKey = "hlcms_user_id";
    private const string RoleKey = "hlcms_role";
    private const string NameKey = "hlcms_name";
    private const string EmailKey = "hlcms_email";
    private const string IssuedKey = "hlcms_issued_utc";
    private const string ChildKey = "hlcms_selected_student";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    private DateTime _lastTouch = DateTime.MinValue;

    public event Action? Changed;

    public HirayaUser? CurrentUser { get; private set; }

    public string? SelectedStudentId { get; private set; }

    public bool IsLoggedIn => CurrentUser != null;

    public void Restore()
    {
        var id = Preferences.Default.Get(UserIdKey, "");
        if (string.IsNullOrEmpty(id)) return;
        if (!TryReadIssued(out var issued) || DateTime.UtcNow - issued > Lifetime)
        {
            ClearPreferences();
            return;
        }

        CurrentUser = new HirayaUser
        {
            UserId = id,
            Role = Preferences.Default.Get(RoleKey, ""),
            Fullname = Preferences.Default.Get(NameKey, ""),
            Email = Preferences.Default.Get(EmailKey, ""),
            Status = "active",
            Password = null
        };
        SelectedStudentId = Preferences.Default.Get(ChildKey, "");
        if (string.IsNullOrWhiteSpace(SelectedStudentId))
            SelectedStudentId = null;
    }

    public void SignIn(HirayaUser user)
    {
        CurrentUser = StripSecret(user);
        Preferences.Default.Set(UserIdKey, CurrentUser.UserId);
        Preferences.Default.Set(RoleKey, CurrentUser.Role);
        Preferences.Default.Set(NameKey, CurrentUser.Fullname);
        Preferences.Default.Set(EmailKey, CurrentUser.Email ?? "");
        Preferences.Default.Set(IssuedKey, DateTime.UtcNow.Ticks.ToString());
        _lastTouch = DateTime.UtcNow;
        Changed?.Invoke();
    }

    public void SelectStudent(string? studentId)
    {
        SelectedStudentId = string.IsNullOrWhiteSpace(studentId) ? null : studentId;
        if (SelectedStudentId is null)
            Preferences.Default.Remove(ChildKey);
        else
            Preferences.Default.Set(ChildKey, SelectedStudentId);
        Changed?.Invoke();
    }

    public void SignOut()
    {
        CurrentUser = null;
        SelectedStudentId = null;
        ClearPreferences();
        Changed?.Invoke();
    }

    public async Task<bool> EnsureValidAsync(Func<Task<HirayaDatabase>> load)
    {
        if (CurrentUser == null)
            return false;

        if (!TryReadIssued(out var issued) || DateTime.UtcNow - issued > Lifetime)
        {
            SignOut();
            return false;
        }

        var db = await load();
        var live = db.Users.FirstOrDefault(u => u.UserId == CurrentUser.UserId);
        if (live == null || !string.Equals(live.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            SignOut();
            return false;
        }

        CurrentUser = AccountService.ClonePublic(live);
        Preferences.Default.Set(RoleKey, CurrentUser.Role);
        Preferences.Default.Set(NameKey, CurrentUser.Fullname);
        Preferences.Default.Set(EmailKey, CurrentUser.Email ?? "");
        Touch();
        return true;
    }

    public void Touch()
    {
        if (CurrentUser == null) return;
        if (DateTime.UtcNow - _lastTouch < TimeSpan.FromMinutes(2))
            return;
        _lastTouch = DateTime.UtcNow;
        Preferences.Default.Set(IssuedKey, DateTime.UtcNow.Ticks.ToString());
    }

    public static HirayaUser StripSecret(HirayaUser user) => AccountService.ClonePublic(user);

    private static bool TryReadIssued(out DateTime issued)
    {
        issued = default;
        var raw = Preferences.Default.Get(IssuedKey, "");
        if (!long.TryParse(raw, out var ticks))
            return false;
        try
        {
            issued = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void ClearPreferences()
    {
        Preferences.Default.Remove(UserIdKey);
        Preferences.Default.Remove(RoleKey);
        Preferences.Default.Remove(NameKey);
        Preferences.Default.Remove(EmailKey);
        Preferences.Default.Remove(IssuedKey);
        Preferences.Default.Remove(ChildKey);
    }
}
