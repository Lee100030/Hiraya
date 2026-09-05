using System.Text.Json;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services;
using Hiraya.Shared.Services.Firebase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hiraya.Data;

public class EfHirayaRepository : IHirayaRepository
{
    private readonly IDbContextFactory<HirayaLearningCenterDbContext> _factory;
    private readonly ILogger<EfHirayaRepository> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public EfHirayaRepository(
        IDbContextFactory<HirayaLearningCenterDbContext> factory,
        ILogger<EfHirayaRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public bool UsingFirebase => false;

    public async Task MutateAsync(Func<HirayaDatabase, Task> mutator, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var db = await LoadUnlockedAsync(ct);
            await mutator(db);
            await SaveUnlockedAsync(db, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HirayaDatabase> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await LoadUnlockedAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(HirayaDatabase database, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await SaveUnlockedAsync(database, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetItemAsync<T>(string collection, string id, T item, CancellationToken ct = default) =>
        MutateAsync(_ => Task.CompletedTask, ct);

    public Task DeleteItemAsync(string collection, string id, CancellationToken ct = default) =>
        MutateAsync(_ => Task.CompletedTask, ct);

    private async Task<HirayaDatabase> LoadUnlockedAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var data = new HirayaDatabase
        {
            Users = await db.Users.AsNoTracking().ToListAsync(ct),
            Roles = await db.Roles.AsNoTracking().ToListAsync(ct),
            RolePermissions = await db.RolePermissions.AsNoTracking().ToListAsync(ct),
            TeacherProfiles = await db.TeacherProfiles.AsNoTracking().ToListAsync(ct),
            Students = await db.Students.AsNoTracking().ToListAsync(ct),
            Programs = await db.Programs.AsNoTracking().ToListAsync(ct),
            Classes = await db.Classes.AsNoTracking().ToListAsync(ct),
            Rooms = await db.Rooms.AsNoTracking().ToListAsync(ct),
            Enrollments = await db.Enrollments.AsNoTracking().ToListAsync(ct),
            EnrollmentApplications = await db.EnrollmentApplications.AsNoTracking().ToListAsync(ct),
            Attendance = await db.Attendance.AsNoTracking().ToListAsync(ct),
            Reports = await db.ProgressRecords.AsNoTracking().ToListAsync(ct),
            Schedules = await db.Schedules.AsNoTracking().ToListAsync(ct),
            TeacherWorkShifts = await db.TeacherWorkShifts.AsNoTracking().ToListAsync(ct),
            TeacherRoleOptions = await db.TeacherRoleOptions.AsNoTracking().ToListAsync(ct),
            News = await db.News.AsNoTracking().ToListAsync(ct),
            LeaveRequests = await db.LeaveRequests.AsNoTracking().ToListAsync(ct),
            Payments = await db.Payments.AsNoTracking().ToListAsync(ct),
            Alerts = await db.Notifications.AsNoTracking().ToListAsync(ct),
            AuditLogs = await db.AuditLogs.AsNoTracking().ToListAsync(ct),
            LearningModules = await db.LearningModules.AsNoTracking().ToListAsync(ct),
            LearningModuleVersions = await db.LearningModuleVersions.AsNoTracking().ToListAsync(ct),
            Settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new CenterSettings()
        };
        PermissionCatalog.EnsureRows(data);
        EnsureRoles(data);
        EnsureRooms(data);
        return Clone(data);
    }

    private async Task SaveUnlockedAsync(HirayaDatabase incoming, CancellationToken ct)
    {
        var data = Clone(incoming);
        EnsureRoles(data);
        EnsureRooms(data);
        PermissionCatalog.EnsureRows(data);
        if (string.IsNullOrWhiteSpace(data.Settings.SettingsId))
            data.Settings.SettingsId = "center";

        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var hashes = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.UserId, u => u.Password, ct);
            foreach (var user in data.Users)
            {
                if (string.IsNullOrWhiteSpace(user.Password) && hashes.TryGetValue(user.UserId, out var hash))
                    user.Password = hash;
            }

            await ReplaceAsync(db.Users, data.Users, ct);
            await ReplaceAsync(db.Roles, data.Roles, ct);
            await ReplaceAsync(db.RolePermissions, data.RolePermissions, ct);
            await ReplaceAsync(db.TeacherProfiles, data.TeacherProfiles, ct);
            await ReplaceAsync(db.Students, data.Students, ct);
            await ReplaceAsync(db.Programs, data.Programs, ct);
            await ReplaceAsync(db.Classes, data.Classes, ct);
            await ReplaceAsync(db.Rooms, data.Rooms, ct);
            await ReplaceAsync(db.Enrollments, data.Enrollments, ct);
            await ReplaceAsync(db.EnrollmentApplications, data.EnrollmentApplications, ct);
            await ReplaceAsync(db.Attendance, data.Attendance, ct);
            await ReplaceAsync(db.ProgressRecords, data.Reports, ct);
            await ReplaceAsync(db.Schedules, data.Schedules, ct);
            await ReplaceAsync(db.TeacherWorkShifts, data.TeacherWorkShifts, ct);
            await ReplaceAsync(db.TeacherRoleOptions, data.TeacherRoleOptions, ct);
            await ReplaceAsync(db.News, data.News, ct);
            await ReplaceAsync(db.LeaveRequests, data.LeaveRequests, ct);
            await ReplaceAsync(db.Payments, data.Payments, ct);
            await ReplaceAsync(db.Notifications, data.Alerts, ct);
            await ReplaceAsync(db.AuditLogs, data.AuditLogs, ct);
            await ReplaceAsync(db.LearningModules, data.LearningModules, ct);
            await ReplaceAsync(db.LearningModuleVersions, data.LearningModuleVersions, ct);

            var settings = await db.Settings.ToListAsync(ct);
            db.Settings.RemoveRange(settings);
            db.Settings.Add(data.Settings);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed saving HLCMS store to MySQL.");
            throw new InvalidOperationException("The database could not save those changes. Check that MySQL is running and try again.");
        }
    }

    private static async Task ReplaceAsync<T>(DbSet<T> set, List<T> next, CancellationToken ct) where T : class
    {
        var current = await set.ToListAsync(ct);
        set.RemoveRange(current);
        if (next.Count > 0)
            await set.AddRangeAsync(next, ct);
    }

    private static void EnsureRoles(HirayaDatabase data)
    {
        if (data.Roles.Count > 0)
            return;
        data.Roles = UserRoles.All.Select(code => new AppRole
        {
            RoleId = "role_" + code,
            Code = code,
            Name = Navigation.RoleLabel(code)
        }).ToList();
    }

    private static void EnsureRooms(HirayaDatabase data)
    {
        var names = data.Classes.Select(c => c.Room)
            .Concat(data.Schedules.Select(s => s.Room))
            .Concat(data.Rooms.Select(r => r.RoomName))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var name in names)
        {
            if (data.Rooms.Any(r => string.Equals(r.RoomName, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            data.Rooms.Add(new CenterRoom
            {
                RoomId = IdFactory.New("room"),
                RoomName = name.Trim(),
                RoomNumber = name.Trim(),
                Status = "active"
            });
        }
    }

    private static HirayaDatabase Clone(HirayaDatabase db)
    {
        var json = JsonSerializer.Serialize(db, JsonDefaults.Options);
        return JsonSerializer.Deserialize<HirayaDatabase>(json, JsonDefaults.Options) ?? new HirayaDatabase();
    }
}
