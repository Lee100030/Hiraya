using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class AuditService
{
    public const int MaxEntries = 500;

    private readonly IHirayaRepository _repository;

    public AuditService(IHirayaRepository repository) => _repository = repository;

    public static void Append(
        HirayaDatabase db,
        HirayaUser? actor,
        string module,
        string action,
        string entityType,
        string entityId,
        string summary)
    {
        db.AuditLogs.Add(new AuditLogEntry
        {
            AuditId = IdFactory.New("aud"),
            ActorId = actor?.UserId ?? "",
            ActorName = actor?.Fullname ?? "System",
            Module = module,
            Action = action,
            EntityType = entityType,
            EntityId = entityId ?? "",
            Summary = summary.Trim(),
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        Trim(db);
    }

    public static void Append(
        HirayaDatabase db,
        string actorId,
        string actorName,
        string module,
        string action,
        string entityType,
        string entityId,
        string summary)
    {
        var actor = string.IsNullOrWhiteSpace(actorId)
            ? null
            : new HirayaUser { UserId = actorId, Fullname = actorName };
        Append(db, actor, module, action, entityType, entityId, summary);
    }

    public static IEnumerable<AuditLogEntry> Visible(HirayaDatabase db) =>
        db.AuditLogs.OrderByDescending(a => a.CreatedAt);

    public async Task RecordAsync(
        HirayaUser? actor,
        string module,
        string action,
        string entityType,
        string entityId,
        string summary)
    {
        await _repository.MutateAsync(async db =>
        {
            Append(db, actor, module, action, entityType, entityId, summary);
            await Task.CompletedTask;
        });
    }

    public async Task RecordLoginAsync(HirayaUser user)
    {
        await RecordAsync(user, AuditModules.Auth, "login", "user", user.UserId, $"{user.Fullname} signed in.");
    }

    public async Task RecordFailedLoginAsync(string login)
    {
        var label = string.IsNullOrWhiteSpace(login) ? "(blank)" : login.Trim();
        await RecordAsync(null, AuditModules.Auth, "login_failed", "user", "", $"Failed sign-in for {label}.");
    }

    private static void Trim(HirayaDatabase db)
    {
        if (db.AuditLogs.Count <= MaxEntries)
            return;
        db.AuditLogs = db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxEntries)
            .ToList();
    }
}

public class NamedCount
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public class NamedAmount
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
}

public class OperationsReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int ActiveStudents { get; set; }
    public int AttendancePresent { get; set; }
    public int AttendanceAbsent { get; set; }
    public int AttendanceLate { get; set; }
    public int AttendanceExcused { get; set; }
    public int AttendanceTotal { get; set; }
    public double AttendanceRate { get; set; }
    public int EnrollmentsActive { get; set; }
    public int EnrollmentsPending { get; set; }
    public int ApplicationsPending { get; set; }
    public decimal PaymentsBilled { get; set; }
    public decimal PaymentsCollected { get; set; }
    public decimal PaymentsOpen { get; set; }
    public int PaymentsOverdue { get; set; }
    public int ProgressPublished { get; set; }
    public int ProgressDraft { get; set; }
    public List<NamedCount> AttendanceByClass { get; set; } = [];
    public List<NamedCount> EnrollmentByStatus { get; set; } = [];
    public List<NamedCount> ProgressByArea { get; set; } = [];
    public List<NamedAmount> PaymentsByStatus { get; set; } = [];
}

public class ReportService
{
    public static OperationsReport Build(HirayaDatabase db, HirayaUser user, DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start)
            (start, end) = (end, start);

        var studentIds = VisibleStudentIds(db, user);

        var students = db.Students.Where(s => studentIds.Contains(s.StudentId)).ToList();
        var attendance = db.Attendance.Where(a =>
            a.Kind != AttendanceKinds.Employee &&
            studentIds.Contains(a.StudentId) &&
            InRange(AttendanceService.DateKey(a.AttendanceDate), start, end)).ToList();
        var enrollments = db.Enrollments.Where(e => studentIds.Contains(e.StudentId)).ToList();
        var payments = PaymentService.Visible(db, user).ToList();
        var progress = ProgressService.Visible(db, user).ToList();

        var present = attendance.Count(a => AttendanceStatuses.IsPresentLike(a.Status) || string.IsNullOrEmpty(a.Status));
        var absent = attendance.Count(a => a.Status == AttendanceStatuses.Absent);
        var late = attendance.Count(a => a.Status == AttendanceStatuses.Late);
        var excused = attendance.Count(a => a.Status == AttendanceStatuses.Excused);

        var report = new OperationsReport
        {
            From = start,
            To = end,
            ActiveStudents = students.Count(s =>
                string.Equals(s.Status, "active", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(s.Status)),
            AttendancePresent = present,
            AttendanceAbsent = absent,
            AttendanceLate = late,
            AttendanceExcused = excused,
            AttendanceTotal = attendance.Count,
            AttendanceRate = attendance.Count == 0 ? 0 : Math.Round(100.0 * present / attendance.Count, 1),
            EnrollmentsActive = enrollments.Count(e => e.Status == EnrollmentStatuses.Active),
            EnrollmentsPending = enrollments.Count(e => e.Status == EnrollmentStatuses.Pending),
            ApplicationsPending = user.Role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff
                ? db.EnrollmentApplications.Count(a => a.Status == "pending_review")
                : 0,
            PaymentsBilled = payments.Sum(p => p.Amount),
            PaymentsCollected = payments.Sum(p => p.AmountPaid > 0 ? p.AmountPaid : p.PaymentStatus == PaymentStatuses.Paid ? p.Amount : 0),
            PaymentsOpen = payments.Sum(PaymentService.Remaining),
            PaymentsOverdue = payments.Count(p => PaymentService.EffectiveStatus(p) == PaymentStatuses.Overdue),
            ProgressPublished = progress.Count(r => r.Status == ProgressStatuses.Published),
            ProgressDraft = progress.Count(r => r.Status == ProgressStatuses.Draft)
        };

        report.AttendanceByClass = attendance
            .GroupBy(a => string.IsNullOrWhiteSpace(a.ClassId) ? "—" : db.Classes.FirstOrDefault(c => c.ClassId == a.ClassId)?.Name ?? a.ClassId)
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        report.EnrollmentByStatus = enrollments
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Status) ? "unknown" : e.Status)
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        report.ProgressByArea = progress
            .GroupBy(r => ProgressAreas.Label(r.Area))
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        report.PaymentsByStatus = payments
            .GroupBy(p => PaymentService.EffectiveStatus(p))
            .Select(g => new NamedAmount
            {
                Name = g.Key,
                Amount = g.Sum(p => PaymentStatuses.IsOpen(g.Key) ? PaymentService.Remaining(p) : p.AmountPaid > 0 ? p.AmountPaid : p.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return report;
    }

    private static HashSet<string> VisibleStudentIds(HirayaDatabase db, HirayaUser user)
    {
        if (user.Role == UserRoles.Parent)
            return db.Students.Where(s => s.ParentId == user.UserId).Select(s => s.StudentId).ToHashSet();
        if (user.Role == UserRoles.Teacher)
        {
            return db.Enrollments
                .Where(e => e.TeacherId == user.UserId ||
                            db.Classes.Any(c => c.ClassId == e.ClassId && c.TeacherId == user.UserId))
                .Select(e => e.StudentId)
                .ToHashSet();
        }

        return db.Students.Select(s => s.StudentId).ToHashSet();
    }

    private static bool InRange(string dateKey, DateTime start, DateTime end)
    {
        if (!DateTime.TryParse(dateKey, out var day))
            return false;
        var d = day.Date;
        return d >= start && d <= end;
    }
}
