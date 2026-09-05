using Hiraya.Shared.Models;

namespace Hiraya.Shared.Services;

public static class StoreSanitizer
{
    public static HirayaDatabase PublicCopy(HirayaDatabase source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source, Firebase.JsonDefaults.Options);
        var copy = System.Text.Json.JsonSerializer.Deserialize<HirayaDatabase>(json, Firebase.JsonDefaults.Options)
                   ?? new HirayaDatabase();
        foreach (var user in copy.Users)
            user.Password = null;
        return copy;
    }
}

public sealed class LoginRequest
{
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class PasswordChangeRequest
{
    public string UserId { get; set; } = "";
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}
