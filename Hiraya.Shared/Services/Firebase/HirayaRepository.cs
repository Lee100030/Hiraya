using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Hiraya.Shared.Services.Firebase;

public interface IHirayaRepository
{
    bool UsingFirebase { get; }
    Task<HirayaDatabase> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(HirayaDatabase database, CancellationToken ct = default);
    Task SetItemAsync<T>(string collection, string id, T item, CancellationToken ct = default);
    Task DeleteItemAsync(string collection, string id, CancellationToken ct = default);
    Task MutateAsync(Func<HirayaDatabase, Task> mutator, CancellationToken ct = default);
}

public class HirayaRepository : IHirayaRepository
{
    private readonly FirebaseCredentialProvider _credentials;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHirayaAppPaths _paths;
    private readonly ILogger<HirayaRepository> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HirayaDatabase? _localCache;
    private readonly string _localPath;

    public HirayaRepository(
        FirebaseCredentialProvider credentials,
        IHttpClientFactory httpClientFactory,
        IHirayaAppPaths paths,
        ILogger<HirayaRepository> logger)
    {
        _credentials = credentials;
        _httpClientFactory = httpClientFactory;
        _paths = paths;
        _logger = logger;
        _localPath = Path.Combine(_paths.DataDirectory, "hiraya-db.json");
    }

    public bool UsingFirebase => _credentials.IsConfigured;

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

    public async Task SetItemAsync<T>(string collection, string id, T item, CancellationToken ct = default)
    {
        await MutateAsync(db =>
        {
            UpsertLocal(db, collection, id, item!);
            return Task.CompletedTask;
        }, ct);
    }

    public async Task DeleteItemAsync(string collection, string id, CancellationToken ct = default)
    {
        await MutateAsync(db =>
        {
            RemoveLocal(db, collection, id);
            return Task.CompletedTask;
        }, ct);
    }

    private async Task<HirayaDatabase> LoadUnlockedAsync(CancellationToken ct)
    {
        if (UsingFirebase)
        {
            try
            {
                var raw = await GetJsonAsync(HirayaCollections.Root, ct);
                if (raw is null || raw.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    var seed = SeedData.Create();
                    await SaveUnlockedAsync(seed, ct);
                    _localCache = seed;
                    return Clone(seed);
                }

                var db = ParseDatabase(raw.Value);
                _localCache = db;
                return Clone(db);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading from Firebase RTDB. Falling back to local store.");
            }
        }

        if (_localCache != null) return Clone(_localCache);

        if (File.Exists(_localPath))
        {
            var json = await File.ReadAllTextAsync(_localPath, ct);
            _localCache = JsonSerializer.Deserialize<HirayaDatabase>(json, JsonDefaults.Options) ?? SeedData.Create();
            return Clone(_localCache);
        }

        _localCache = SeedData.Create();
        await WriteLocalFileAsync(_localCache, ct);
        return Clone(_localCache);
    }

    private async Task SaveUnlockedAsync(HirayaDatabase database, CancellationToken ct)
    {
        _localCache = Clone(database);
        await WriteLocalFileAsync(_localCache, ct);

        if (!UsingFirebase) return;

        var payload = new Dictionary<string, object?>
        {
            [HirayaCollections.Users] = ToRecord(database.Users, u => u.UserId),
            [HirayaCollections.TeacherProfiles] = ToRecord(database.TeacherProfiles, t => t.ProfileId),
            [HirayaCollections.Students] = ToRecord(database.Students, s => s.StudentId),
            [HirayaCollections.Programs] = ToRecord(database.Programs, p => p.ProgramId),
            [HirayaCollections.Classes] = ToRecord(database.Classes, c => c.ClassId),
            [HirayaCollections.Enrollments] = ToRecord(database.Enrollments, e => e.EnrollmentId),
            [HirayaCollections.EnrollmentApplications] = ToRecord(database.EnrollmentApplications, a => a.ApplicationId),
            [HirayaCollections.Attendance] = ToRecord(database.Attendance, a => a.AttendanceId),
            [HirayaCollections.Reports] = ToRecord(database.Reports, r => r.ReportId),
            [HirayaCollections.Schedules] = ToRecord(database.Schedules, s => s.ScheduleId),
            [HirayaCollections.TeacherWorkShifts] = ToRecord(database.TeacherWorkShifts, s => s.ShiftId),
            [HirayaCollections.TeacherRoleOptions] = ToRecord(database.TeacherRoleOptions, r => r.RoleId),
            [HirayaCollections.News] = ToRecord(database.News, n => n.NewsId),
            [HirayaCollections.LeaveRequests] = ToRecord(database.LeaveRequests, l => l.LeaveId),
            [HirayaCollections.Payments] = ToRecord(database.Payments, p => p.PaymentId),
            [HirayaCollections.Alerts] = ToRecord(database.Alerts, a => a.AlertId),
            [HirayaCollections.AuditLogs] = ToRecord(database.AuditLogs, a => a.AuditId),
            [HirayaCollections.RolePermissions] = ToRecord(database.RolePermissions, p => p.PermissionId),
            [HirayaCollections.Settings] = database.Settings,
        };

        await PutJsonAsync(HirayaCollections.Root, payload, ct);
    }

    private async Task WriteLocalFileAsync(HirayaDatabase database, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_localPath)!);
        var json = JsonSerializer.Serialize(database, JsonDefaults.Options);
        await File.WriteAllTextAsync(_localPath, json, ct);
    }

    private async Task<JsonElement?> GetJsonAsync(string path, CancellationToken ct)
    {
        var client = await CreateAuthorizedClientAsync();
        var url = $"{_credentials.DatabaseUrl}/{path}.json";
        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    private async Task PutJsonAsync<T>(string path, T payload, CancellationToken ct)
    {
        var client = await CreateAuthorizedClientAsync();
        var url = $"{_credentials.DatabaseUrl}/{path}.json";
        var json = JsonSerializer.Serialize(payload, JsonDefaults.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var token = await _credentials.GetAccessTokenAsync()
                    ?? throw new InvalidOperationException("Firebase access token unavailable.");
        var client = _httpClientFactory.CreateClient("firebase-rtdb");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Dictionary<string, T> ToRecord<T>(IEnumerable<T> items, Func<T, string> idSelector) =>
        items.Where(x => !string.IsNullOrWhiteSpace(idSelector(x))).ToDictionary(idSelector, x => x);

    private static HirayaDatabase ParseDatabase(JsonElement root) => new()
    {
        Users = ReadCollection<HirayaUser>(root, HirayaCollections.Users),
        TeacherProfiles = ReadCollection<TeacherProfile>(root, HirayaCollections.TeacherProfiles),
        Students = ReadCollection<Student>(root, HirayaCollections.Students),
        Programs = ReadCollection<LearningProgram>(root, HirayaCollections.Programs),
        Classes = ReadCollection<LearningClass>(root, HirayaCollections.Classes),
        Enrollments = ReadCollection<Enrollment>(root, HirayaCollections.Enrollments),
        EnrollmentApplications = ReadCollection<EnrollmentApplication>(root, HirayaCollections.EnrollmentApplications),
        Attendance = ReadCollection<AttendanceRecord>(root, HirayaCollections.Attendance),
        Reports = ReadCollection<ProgressReport>(root, HirayaCollections.Reports),
        Schedules = ReadCollection<Schedule>(root, HirayaCollections.Schedules),
        TeacherWorkShifts = ReadCollection<TeacherWorkShift>(root, HirayaCollections.TeacherWorkShifts),
        TeacherRoleOptions = ReadCollection<TeacherRoleOption>(root, HirayaCollections.TeacherRoleOptions),
        News = ReadCollection<NewsItem>(root, HirayaCollections.News),
        LeaveRequests = ReadCollection<LeaveRequest>(root, HirayaCollections.LeaveRequests),
        Payments = ReadCollection<Payment>(root, HirayaCollections.Payments),
        Alerts = ReadCollection<SystemAlert>(root, HirayaCollections.Alerts),
        AuditLogs = ReadCollection<AuditLogEntry>(root, HirayaCollections.AuditLogs),
        RolePermissions = ReadCollection<RolePermission>(root, HirayaCollections.RolePermissions),
        Settings = ReadSettings(root)
    };

    private static CenterSettings ReadSettings(JsonElement root)
    {
        if (!root.TryGetProperty(HirayaCollections.Settings, out var prop) ||
            prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new CenterSettings();
        return JsonSerializer.Deserialize<CenterSettings>(prop.GetRawText(), JsonDefaults.Options) ?? new CenterSettings();
    }

    private static List<T> ReadCollection<T>(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new List<T>();

        if (prop.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<T>>(prop.GetRawText(), JsonDefaults.Options) ?? new List<T>();

        var dict = JsonSerializer.Deserialize<Dictionary<string, T>>(prop.GetRawText(), JsonDefaults.Options);
        return dict?.Values.ToList() ?? new List<T>();
    }

    private static HirayaDatabase Clone(HirayaDatabase db)
    {
        var json = JsonSerializer.Serialize(db, JsonDefaults.Options);
        return JsonSerializer.Deserialize<HirayaDatabase>(json, JsonDefaults.Options) ?? new HirayaDatabase();
    }

    private static void UpsertLocal(HirayaDatabase db, string collection, string id, object item)
    {
        switch (collection)
        {
            case HirayaCollections.Users when item is HirayaUser u:
                db.Users.RemoveAll(x => x.UserId == id);
                db.Users.Add(u);
                break;
            case HirayaCollections.TeacherProfiles when item is TeacherProfile t:
                db.TeacherProfiles.RemoveAll(x => x.ProfileId == id);
                db.TeacherProfiles.Add(t);
                break;
            case HirayaCollections.Students when item is Student s:
                db.Students.RemoveAll(x => x.StudentId == id);
                db.Students.Add(s);
                break;
            case HirayaCollections.Programs when item is LearningProgram p:
                db.Programs.RemoveAll(x => x.ProgramId == id);
                db.Programs.Add(p);
                break;
            case HirayaCollections.Classes when item is LearningClass c:
                db.Classes.RemoveAll(x => x.ClassId == id);
                db.Classes.Add(c);
                break;
            case HirayaCollections.Enrollments when item is Enrollment e:
                db.Enrollments.RemoveAll(x => x.EnrollmentId == id);
                db.Enrollments.Add(e);
                break;
            case HirayaCollections.EnrollmentApplications when item is EnrollmentApplication a:
                db.EnrollmentApplications.RemoveAll(x => x.ApplicationId == id);
                db.EnrollmentApplications.Add(a);
                break;
            case HirayaCollections.Attendance when item is AttendanceRecord a:
                db.Attendance.RemoveAll(x => x.AttendanceId == id);
                db.Attendance.Add(a);
                break;
            case HirayaCollections.Reports when item is ProgressReport r:
                db.Reports.RemoveAll(x => x.ReportId == id);
                db.Reports.Add(r);
                break;
            case HirayaCollections.Schedules when item is Schedule s:
                db.Schedules.RemoveAll(x => x.ScheduleId == id);
                db.Schedules.Add(s);
                break;
            case HirayaCollections.TeacherWorkShifts when item is TeacherWorkShift s:
                db.TeacherWorkShifts.RemoveAll(x => x.ShiftId == id);
                db.TeacherWorkShifts.Add(s);
                break;
            case HirayaCollections.TeacherRoleOptions when item is TeacherRoleOption r:
                db.TeacherRoleOptions.RemoveAll(x => x.RoleId == id);
                db.TeacherRoleOptions.Add(r);
                break;
            case HirayaCollections.News when item is NewsItem n:
                db.News.RemoveAll(x => x.NewsId == id);
                db.News.Add(n);
                break;
            case HirayaCollections.LeaveRequests when item is LeaveRequest l:
                db.LeaveRequests.RemoveAll(x => x.LeaveId == id);
                db.LeaveRequests.Add(l);
                break;
            case HirayaCollections.Payments when item is Payment p:
                db.Payments.RemoveAll(x => x.PaymentId == id);
                db.Payments.Add(p);
                break;
            case HirayaCollections.Alerts when item is SystemAlert a:
                db.Alerts.RemoveAll(x => x.AlertId == id);
                db.Alerts.Add(a);
                break;
            case HirayaCollections.AuditLogs when item is AuditLogEntry a:
                db.AuditLogs.RemoveAll(x => x.AuditId == id);
                db.AuditLogs.Add(a);
                break;
        }
    }

    private static void RemoveLocal(HirayaDatabase db, string collection, string id)
    {
        switch (collection)
        {
            case HirayaCollections.Users:
                db.Users.RemoveAll(x => x.UserId == id);
                break;
            case HirayaCollections.TeacherProfiles:
                db.TeacherProfiles.RemoveAll(x => x.ProfileId == id);
                break;
            case HirayaCollections.Students:
                db.Students.RemoveAll(x => x.StudentId == id);
                break;
            case HirayaCollections.Programs:
                db.Programs.RemoveAll(x => x.ProgramId == id);
                break;
            case HirayaCollections.Classes:
                db.Classes.RemoveAll(x => x.ClassId == id);
                break;
            case HirayaCollections.Enrollments:
                db.Enrollments.RemoveAll(x => x.EnrollmentId == id);
                break;
            case HirayaCollections.EnrollmentApplications:
                db.EnrollmentApplications.RemoveAll(x => x.ApplicationId == id);
                break;
            case HirayaCollections.Attendance:
                db.Attendance.RemoveAll(x => x.AttendanceId == id);
                break;
            case HirayaCollections.Reports:
                db.Reports.RemoveAll(x => x.ReportId == id);
                break;
            case HirayaCollections.Schedules:
                db.Schedules.RemoveAll(x => x.ScheduleId == id);
                break;
            case HirayaCollections.TeacherWorkShifts:
                db.TeacherWorkShifts.RemoveAll(x => x.ShiftId == id);
                break;
            case HirayaCollections.TeacherRoleOptions:
                db.TeacherRoleOptions.RemoveAll(x => x.RoleId == id);
                break;
            case HirayaCollections.News:
                db.News.RemoveAll(x => x.NewsId == id);
                break;
            case HirayaCollections.LeaveRequests:
                db.LeaveRequests.RemoveAll(x => x.LeaveId == id);
                break;
            case HirayaCollections.Payments:
                db.Payments.RemoveAll(x => x.PaymentId == id);
                break;
            case HirayaCollections.Alerts:
                db.Alerts.RemoveAll(x => x.AlertId == id);
                break;
            case HirayaCollections.AuditLogs:
                db.AuditLogs.RemoveAll(x => x.AuditId == id);
                break;
        }
    }
}
