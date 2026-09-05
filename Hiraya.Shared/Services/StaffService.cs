using Hiraya.Shared.Models;
using Hiraya.Shared.Security;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class StaffSaveRequest
{
    public string UserId { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string Fullname { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = UserRoles.Teacher;
    public string EmployeeCode { get; set; } = "";
    public string Position { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string Bio { get; set; } = "";
    public string Status { get; set; } = "active";
}

public class StaffService
{
    private readonly IHirayaRepository _repository;

    public StaffService(IHirayaRepository repository) => _repository = repository;

    public static void EnsureDefaultSpecialties(HirayaDatabase db)
    {
        foreach (var name in TeacherRoleCatalog.Defaults)
        {
            if (db.TeacherRoleOptions.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            db.TeacherRoleOptions.Add(new TeacherRoleOption
            {
                RoleId = IdFactory.New("trole"),
                Name = name
            });
        }
    }

    public async Task AddSpecialtyAsync(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Enter a specialization name.");

        await _repository.MutateAsync(async db =>
        {
            EnsureDefaultSpecialties(db);
            if (!db.TeacherRoleOptions.Any(r => string.Equals(r.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                db.TeacherRoleOptions.Add(new TeacherRoleOption
                {
                    RoleId = IdFactory.New("trole"),
                    Name = trimmed
                });
            }
            await Task.CompletedTask;
        });
    }

    public async Task SaveAsync(StaffSaveRequest input, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageStaff(actor?.Role))
            throw new InvalidOperationException("You cannot manage teachers or staff.");
        var fullname = input.Fullname.Trim();
        var email = input.Email.Trim();
        var role = input.Role;
        if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Full name and email are required.");
        if (!Navigation.IsEmployeeRole(role))
            throw new InvalidOperationException("Role must be Teacher or Staff.");

        await _repository.MutateAsync(async db =>
        {
            var userId = string.IsNullOrWhiteSpace(input.UserId) ? IdFactory.New("usr") : input.UserId;
            var profileId = string.IsNullOrWhiteSpace(input.ProfileId) ? IdFactory.New("tprof") : input.ProfileId;
            var existing = db.Users.FirstOrDefault(u => u.UserId == userId);

            if (db.Users.Any(u =>
                    u.UserId != userId &&
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That email is already used by another account.");

            var password = input.Password.Trim();
            string storedPassword;
            if (!string.IsNullOrWhiteSpace(password))
                storedPassword = PasswordHasher.Hash(password);
            else if (!string.IsNullOrWhiteSpace(existing?.Password))
                storedPassword = existing.Password!;
            else
                throw new InvalidOperationException("Password is required for a new account.");

            EnsureDefaultSpecialties(db);
            var specialty = input.Specialty.Trim();
            if (!string.IsNullOrWhiteSpace(specialty) &&
                !db.TeacherRoleOptions.Any(r => string.Equals(r.Name, specialty, StringComparison.OrdinalIgnoreCase)))
            {
                db.TeacherRoleOptions.Add(new TeacherRoleOption
                {
                    RoleId = IdFactory.New("trole"),
                    Name = specialty
                });
            }

            var existingProfile = db.TeacherProfiles.FirstOrDefault(p => p.UserId == userId);
            var status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status.Trim();

            db.Users.RemoveAll(u => u.UserId == userId);
            var saved = new HirayaUser
            {
                UserId = userId,
                Fullname = fullname,
                Email = email,
                Password = storedPassword,
                Role = role,
                Status = status,
                Phone = input.Phone.Trim(),
                Username = existing?.Username ?? "",
                FirstName = existing?.FirstName ?? "",
                MiddleName = existing?.MiddleName ?? "",
                LastName = existing?.LastName ?? "",
                Address = existing?.Address ?? "",
                Position = string.IsNullOrWhiteSpace(input.Position) ? existing?.Position ?? "" : input.Position.Trim(),
                ProfileImagePath = existing?.ProfileImagePath ?? "",
                CreatedAt = string.IsNullOrWhiteSpace(existing?.CreatedAt) ? DateTime.UtcNow.ToString("o") : existing!.CreatedAt,
                LastLoginAt = existing?.LastLoginAt ?? "",
                NotifyAttendance = existing?.NotifyAttendance ?? true,
                NotifyPayments = existing?.NotifyPayments ?? true,
                NotifySchedule = existing?.NotifySchedule ?? true,
                NotifyProgress = existing?.NotifyProgress ?? true,
                DateFormat = existing?.DateFormat ?? "MMM d, yyyy",
                TimeFormat = existing?.TimeFormat ?? "HH:mm",
                Theme = existing?.Theme ?? "system"
            };
            saved.SyncDisplayName();
            if (string.IsNullOrWhiteSpace(saved.FirstName) && string.IsNullOrWhiteSpace(saved.LastName))
                saved.Fullname = fullname;
            db.Users.Add(saved);

            db.TeacherProfiles.RemoveAll(p => p.ProfileId == profileId || p.UserId == userId);
            db.TeacherProfiles.Add(new TeacherProfile
            {
                ProfileId = profileId,
                UserId = userId,
                EmployeeCode = string.IsNullOrWhiteSpace(input.EmployeeCode)
                    ? existingProfile?.EmployeeCode ?? userId.ToUpperInvariant()
                    : input.EmployeeCode.Trim(),
                Position = input.Position.Trim(),
                Specialty = specialty,
                Phone = input.Phone.Trim(),
                Bio = input.Bio.Trim(),
                HireDate = existingProfile?.HireDate ?? DateTime.UtcNow.ToString("o"),
                Status = status
            });

            AuditService.Append(db, actor, AuditModules.Staff,
                string.IsNullOrWhiteSpace(input.UserId) ? "create" : "update",
                "user", userId, $"Saved {fullname} ({role}).");
            await Task.CompletedTask;
        });
    }

    public async Task ArchiveAsync(string userId, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageStaff(actor?.Role))
            throw new InvalidOperationException("You cannot manage teachers or staff.");
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId);
            if (user != null)
                user.Status = "archived";
            foreach (var profile in db.TeacherProfiles.Where(p => p.UserId == userId))
                profile.Status = "archived";
            AuditService.Append(db, actor, AuditModules.Staff, "archive", "user", userId, $"Archived {user?.Fullname ?? userId}.");
            await Task.CompletedTask;
        });
    }

    public async Task RestoreAsync(string userId, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageStaff(actor?.Role))
            throw new InvalidOperationException("You cannot manage teachers or staff.");
        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == userId)
                       ?? throw new InvalidOperationException("Account was not found.");
            user.Status = "active";
            foreach (var profile in db.TeacherProfiles.Where(p => p.UserId == userId))
                profile.Status = "active";
            AuditService.Append(db, actor, AuditModules.Staff, "restore", "user", userId, $"Restored {user.Fullname}.");
            await Task.CompletedTask;
        });
    }
}
