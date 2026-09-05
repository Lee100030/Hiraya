using System.Net.Http.Json;
using Hiraya.Shared;
using Hiraya.Shared.Models;
using Hiraya.Shared.Security;
using Hiraya.Shared.Services.Firebase;
using Microsoft.Extensions.Options;

namespace Hiraya.Shared.Services;

public class ProfileSaveRequest
{
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Position { get; set; } = "";
    public bool NotifyAttendance { get; set; } = true;
    public bool NotifyPayments { get; set; } = true;
    public bool NotifySchedule { get; set; } = true;
    public bool NotifyProgress { get; set; } = true;
    public string DateFormat { get; set; } = "MMM d, yyyy";
    public string TimeFormat { get; set; } = "HH:mm";
    public string Theme { get; set; } = "system";
}

public class AccountService
{
    private readonly IHirayaRepository _repository;
    private readonly IHttpClientFactory _http;
    private readonly HirayaApiOptions _api;

    public AccountService(
        IHirayaRepository repository,
        IHttpClientFactory http,
        IOptions<HirayaApiOptions> api)
    {
        _repository = repository;
        _http = http;
        _api = api.Value;
    }

    public async Task<HirayaUser> SaveProfileAsync(string userId, ProfileSaveRequest input)
    {
        HirayaUser? saved = null;
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId)
                       ?? throw new InvalidOperationException("Account was not found.");
            if (!string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This account is not active.");

            var email = input.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email is required.");
            if (db.Users.Any(u =>
                    u.UserId != userId &&
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That email is already used by another account.");

            var username = input.Username.Trim();
            if (!string.IsNullOrWhiteSpace(username) &&
                db.Users.Any(u =>
                    u.UserId != userId &&
                    string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That username is already taken.");

            user.FirstName = input.FirstName.Trim();
            user.MiddleName = input.MiddleName.Trim();
            user.LastName = input.LastName.Trim();
            user.Username = username;
            user.Email = email;
            user.Phone = input.Phone.Trim();
            user.Address = input.Address.Trim();
            user.Position = input.Position.Trim();
            user.NotifyAttendance = input.NotifyAttendance;
            user.NotifyPayments = input.NotifyPayments;
            user.NotifySchedule = input.NotifySchedule;
            user.NotifyProgress = input.NotifyProgress;
            user.DateFormat = string.IsNullOrWhiteSpace(input.DateFormat) ? "MMM d, yyyy" : input.DateFormat.Trim();
            user.TimeFormat = string.IsNullOrWhiteSpace(input.TimeFormat) ? "HH:mm" : input.TimeFormat.Trim();
            user.Theme = string.IsNullOrWhiteSpace(input.Theme) ? "system" : input.Theme.Trim();
            user.SyncDisplayName();
            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            AuditService.Append(db, user, AuditModules.Settings, "update", "user", user.UserId,
                $"{user.Fullname} updated their profile.");
            saved = ClonePublic(user);
            await Task.CompletedTask;
        });
        return saved!;
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, string confirmPassword)
    {
        var problem = PasswordRules.ValidateNew(currentPassword, newPassword, confirmPassword);
        if (problem != null)
            throw new InvalidOperationException(problem);
        if (_api.UseRemoteStore)
        {
            var client = _http.CreateClient("hiraya-api");
            using var response = await client.PostAsJsonAsync("api/auth/password", new PasswordChangeRequest
            {
                UserId = userId,
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmPassword = confirmPassword
            });
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? "Password could not be changed." : body.Trim('"'));
            }
            return;
        }
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId)
                       ?? throw new InvalidOperationException("Account was not found.");
            if (!PasswordHasher.Verify(currentPassword, user.Password))
                throw new InvalidOperationException("Current password is incorrect.");
            user.Password = PasswordHasher.Hash(newPassword);
            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            AuditService.Append(db, ClonePublic(user), AuditModules.Auth, "password_change", "user", user.UserId,
                $"{user.Fullname} changed their password.");
            await Task.CompletedTask;
        });
    }

    public async Task SetProfileImageAsync(string userId, string relativePath)
    {
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId)
                       ?? throw new InvalidOperationException("Account was not found.");
            user.ProfileImagePath = relativePath;
            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            AuditService.Append(db, ClonePublic(user), AuditModules.Settings, "photo", "user", user.UserId,
                string.IsNullOrEmpty(relativePath)
                    ? $"{user.Fullname} removed their profile picture."
                    : $"{user.Fullname} updated their profile picture.");
            await Task.CompletedTask;
        });
    }

    public static HirayaUser ClonePublic(HirayaUser user)
    {
        var copy = new HirayaUser
        {
            UserId = user.UserId,
            Username = user.Username,
            FirstName = user.FirstName,
            MiddleName = user.MiddleName,
            LastName = user.LastName,
            Fullname = user.Fullname,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status,
            Phone = user.Phone,
            Address = user.Address,
            Position = user.Position,
            ProfileImagePath = user.ProfileImagePath,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            NotifyAttendance = user.NotifyAttendance,
            NotifyPayments = user.NotifyPayments,
            NotifySchedule = user.NotifySchedule,
            NotifyProgress = user.NotifyProgress,
            DateFormat = user.DateFormat,
            TimeFormat = user.TimeFormat,
            Theme = user.Theme,
            Password = null
        };
        copy.SyncDisplayName();
        return copy;
    }
}
