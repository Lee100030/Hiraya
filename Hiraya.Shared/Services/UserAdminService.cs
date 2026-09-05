using Hiraya.Shared.Models;
using Hiraya.Shared.Security;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class UserAdminSaveRequest
{
    public string UserId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Position { get; set; } = "";
    public string Role { get; set; } = UserRoles.Staff;
    public string Status { get; set; } = "active";
    public string Password { get; set; } = "";
}

public class UserAdminService
{
    private readonly IHirayaRepository _repository;

    public UserAdminService(IHirayaRepository repository) => _repository = repository;

    public async Task SaveAsync(UserAdminSaveRequest input, HirayaUser actor)
    {
        if (!Navigation.CanManageUsers(actor.Role))
            throw new InvalidOperationException("You cannot manage users.");
        if (!Navigation.CanAssignRole(actor.Role, input.Role))
            throw new InvalidOperationException("You cannot assign that role.");

        await _repository.MutateAsync(async db =>
        {
            PermissionCatalog.EnsureRows(db);
            var userId = string.IsNullOrWhiteSpace(input.UserId) ? IdFactory.New("usr") : input.UserId;
            var existing = db.Users.FirstOrDefault(u => u.UserId == userId);
            if (existing != null && !Navigation.CanAdministerAccount(actor.Role, existing.Role))
                throw new InvalidOperationException("You cannot modify a SuperAdmin account.");

            var email = input.Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email is required.");
            if (db.Users.Any(u =>
                    u.UserId != userId &&
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That email is already used.");

            var password = input.Password.Trim();
            string stored;
            if (!string.IsNullOrWhiteSpace(password))
            {
                if (password.Length < PasswordRules.MinimumLength)
                    throw new InvalidOperationException($"Password must be at least {PasswordRules.MinimumLength} characters.");
                stored = PasswordHasher.Hash(password);
            }
            else if (!string.IsNullOrWhiteSpace(existing?.Password))
                stored = existing.Password!;
            else
                throw new InvalidOperationException("Password is required for a new account.");

            var user = existing ?? new HirayaUser { UserId = userId, CreatedAt = DateTime.UtcNow.ToString("o") };
            user.UserId = userId;
            user.FirstName = input.FirstName.Trim();
            user.MiddleName = input.MiddleName.Trim();
            user.LastName = input.LastName.Trim();
            user.Username = input.Username.Trim();
            user.Email = email;
            user.Phone = input.Phone.Trim();
            user.Address = input.Address.Trim();
            user.Position = input.Position.Trim();
            user.Role = input.Role;
            user.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status.Trim();
            user.Password = stored;
            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrWhiteSpace(user.CreatedAt))
                user.CreatedAt = DateTime.UtcNow.ToString("o");
            user.SyncDisplayName();
            if (string.IsNullOrWhiteSpace(user.Fullname))
                throw new InvalidOperationException("Name is required.");

            db.Users.RemoveAll(u => u.UserId == userId);
            db.Users.Add(user);
            AuditService.Append(db, actor, AuditModules.Users,
                existing == null ? "create" : "update",
                "user", userId, $"{actor.Fullname} saved user {user.Fullname} ({Navigation.RoleLabel(user.Role)}).");
            await Task.CompletedTask;
        });
    }

    public async Task SetActiveAsync(string userId, bool active, HirayaUser actor)
    {
        if (!Navigation.CanManageUsers(actor.Role))
            throw new InvalidOperationException("You cannot manage users.");
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId)
                       ?? throw new InvalidOperationException("User was not found.");
            if (!Navigation.CanAdministerAccount(actor.Role, user.Role))
                throw new InvalidOperationException("You cannot modify a SuperAdmin account.");
            if (!active && user.Role == UserRoles.SuperAdmin &&
                db.Users.Count(u => u.Role == UserRoles.SuperAdmin && string.Equals(u.Status, "active", StringComparison.OrdinalIgnoreCase)) <= 1)
                throw new InvalidOperationException("Keep at least one active SuperAdmin.");

            user.Status = active ? "active" : "archived";
            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            AuditService.Append(db, actor, AuditModules.Users, active ? "activate" : "deactivate",
                "user", userId, $"{actor.Fullname} set {user.Fullname} to {user.Status}.");
            await Task.CompletedTask;
        });
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, HirayaUser actor)
    {
        if (!Navigation.CanManageUsers(actor.Role))
            throw new InvalidOperationException("You cannot manage users.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < PasswordRules.MinimumLength)
            throw new InvalidOperationException($"Password must be at least {PasswordRules.MinimumLength} characters.");
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId)
                       ?? throw new InvalidOperationException("User was not found.");
            if (!Navigation.CanAdministerAccount(actor.Role, user.Role))
                throw new InvalidOperationException("You cannot modify a SuperAdmin account.");
            user.Password = PasswordHasher.Hash(newPassword);
            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            AuditService.Append(db, actor, AuditModules.Users, "reset_password", "user", userId,
                $"{actor.Fullname} reset the password for {user.Fullname}.");
            await Task.CompletedTask;
        });
    }

    public async Task SavePermissionAsync(string role, string permission, bool allowed, HirayaUser actor)
    {
        if (!Navigation.CanManageRoles(actor.Role))
            throw new InvalidOperationException("Only a SuperAdmin can change role permissions.");
        await _repository.MutateAsync(async db =>
        {
            PermissionCatalog.EnsureRows(db);
            var row = db.RolePermissions.FirstOrDefault(p => p.Role == role && p.Permission == permission)
                      ?? throw new InvalidOperationException("Permission row was not found.");
            row.Allowed = allowed;
            AuditService.Append(db, actor, AuditModules.Roles, "update", "role", role,
                $"{actor.Fullname} set {Navigation.RoleLabel(role)} / {permission} to {(allowed ? "allowed" : "denied")}.");
            await Task.CompletedTask;
        });
    }
}
