namespace Hiraya.Shared.Security;

public static class PasswordRules
{
    public const int MinimumLength = 8;

    public static string? ValidateNew(string current, string next, string confirm)
    {
        if (string.IsNullOrWhiteSpace(next) || next.Length < MinimumLength)
            return $"New password must be at least {MinimumLength} characters.";
        if (!string.Equals(next, confirm, StringComparison.Ordinal))
            return "New password and confirmation do not match.";
        if (string.Equals(current, next, StringComparison.Ordinal))
            return "New password must be different from the current password.";
        return null;
    }
}
