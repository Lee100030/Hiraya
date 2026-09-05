using Hiraya.Shared.Models;

namespace Hiraya.Shared.Services;

public sealed class FamilyHomeSnapshot
{
    public string CenterName { get; init; } = "";
    public string Greeting { get; init; } = "";
    public IReadOnlyList<Student> Children { get; init; } = [];
    public IReadOnlyList<CalendarItem> Today { get; init; } = [];
    public IReadOnlyList<CalendarItem> Upcoming { get; init; } = [];
    public IReadOnlyList<FamilyAttendanceRow> AttendanceToday { get; init; } = [];
    public int OpenPayments { get; init; }
    public int OverduePayments { get; init; }
    public decimal OpenBalance { get; init; }
    public string PaymentHeadline { get; init; } = "No bills";
    public int AttendancePercent { get; init; }
    public string ProgressHeadline { get; init; } = "No notes yet";
    public CalendarItem? NextClass { get; init; }
    public Student? FocusChild { get; init; }
    public IReadOnlyList<ProgressReport> RecentProgress { get; init; } = [];
    public IReadOnlyList<NewsItem> Announcements { get; init; } = [];
    public IReadOnlyList<SystemAlert> Alerts { get; init; } = [];
}

public sealed class FamilyAttendanceRow
{
    public Student Student { get; init; } = new();
    public AttendanceRecord? Record { get; init; }
}

public static class FamilyPortal
{
    public static IReadOnlyList<Student> VisibleStudents(HirayaDatabase db, HirayaUser user)
    {
        if (user.Role == UserRoles.Parent)
        {
            return db.Students
                .Where(s => s.ParentId == user.UserId)
                .OrderBy(StudentNames.Display)
                .ToList();
        }

        if (user.Role == UserRoles.Student)
        {
            return db.Students
                .Where(s =>
                    string.Equals(s.StudentId, user.UserId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.ContactNumber, user.Phone, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(user.Phone))
                .OrderBy(StudentNames.Display)
                .ToList();
        }

        return [];
    }

    public static HashSet<string> StudentIds(HirayaDatabase db, HirayaUser user) =>
        VisibleStudents(db, user).Select(s => s.StudentId).ToHashSet();

    public static string AttendanceLabel(string? status) => status switch
    {
        AttendanceStatuses.Present => "Present",
        AttendanceStatuses.Absent => "Absent",
        AttendanceStatuses.Late => "Late",
        AttendanceStatuses.Excused => "Excused",
        AttendanceStatuses.Leave => "Leave",
        _ => "Not recorded"
    };

    public static string PaymentLabel(string? status) => status switch
    {
        PaymentStatuses.Pending => "Pending",
        PaymentStatuses.Partial => "Partial",
        PaymentStatuses.Overdue => "Overdue",
        PaymentStatuses.Paid => "Paid",
        PaymentStatuses.Failed => "Failed",
        PaymentStatuses.Refunded => "Refunded",
        PaymentStatuses.Cancelled => "Cancelled",
        _ => string.IsNullOrWhiteSpace(status) ? "Unknown" : status
    };

    public static IReadOnlyList<CalendarItem> FamilyCalendar(HirayaDatabase db, HirayaUser user, string? selectedStudentId = null)
    {
        var children = VisibleStudents(db, user);
        var focus = children;
        if (!string.IsNullOrWhiteSpace(selectedStudentId))
            focus = children.Where(s => s.StudentId == selectedStudentId).ToList();
        if (focus.Count == 0)
            focus = children;

        var focusIds = focus.Select(s => s.StudentId).ToHashSet();
        var classIds = db.Enrollments
            .Where(e => focusIds.Contains(e.StudentId) && EnrollmentStatuses.CountsTowardCapacity(e.Status))
            .Select(e => e.ClassId)
            .ToHashSet();

        return ScheduleService.VisibleItems(db, user)
            .Where(i => i.Kind is not ScheduleKinds.Deadline and not ScheduleKinds.TeacherTask)
            .Where(i =>
                focusIds.Count == 0 ||
                focusIds.Contains(i.StudentId) ||
                (!string.IsNullOrWhiteSpace(i.ClassId) && classIds.Contains(i.ClassId)) ||
                (string.IsNullOrWhiteSpace(i.StudentId) && string.IsNullOrWhiteSpace(i.ClassId) && children.Count <= 1))
            .OrderBy(i => i.Start)
            .ToList();
    }

    public static FamilyHomeSnapshot BuildHome(HirayaDatabase db, HirayaUser user, string? selectedStudentId = null)
    {
        var children = VisibleStudents(db, user);
        var focus = children;
        if (!string.IsNullOrWhiteSpace(selectedStudentId))
            focus = children.Where(s => s.StudentId == selectedStudentId).ToList();
        if (focus.Count == 0)
            focus = children;

        var focusIds = focus.Select(s => s.StudentId).ToHashSet();

        var today = DateTime.Now.Date;
        var todayKey = today.ToString("yyyy-MM-dd");
        var schedule = FamilyCalendar(db, user, selectedStudentId);

        var attendance = focus.Select(student => new FamilyAttendanceRow
        {
            Student = student,
            Record = db.Attendance
                .Where(a =>
                    a.Kind != AttendanceKinds.Employee &&
                    a.StudentId == student.StudentId &&
                    AttendanceService.DateKey(a.AttendanceDate) == todayKey)
                .OrderByDescending(a => a.AttendanceDate)
                .FirstOrDefault()
        }).ToList();

        var payments = PaymentService.Visible(db, user)
            .Where(p => focusIds.Count == 0 || focusIds.Contains(PaymentService.ResolveStudentId(db, p)))
            .ToList();
        var open = payments.Where(p => PaymentStatuses.IsOpen(PaymentService.EffectiveStatus(p))).ToList();

        var recentProgress = ProgressService.Visible(db, user)
            .Where(r => focusIds.Count == 0 || focusIds.Contains(r.StudentId))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(5)
            .ToList();

        var history = db.Attendance
            .Where(a => a.Kind != AttendanceKinds.Employee && focusIds.Contains(a.StudentId))
            .OrderByDescending(a => a.AttendanceDate)
            .Take(20)
            .ToList();
        var present = history.Count(a => AttendanceStatuses.IsPresentLike(a.Status));
        var percent = history.Count == 0 ? 0 : (int)Math.Round(100d * present / history.Count);

        var next = schedule.FirstOrDefault(i => i.Start >= DateTime.Now);
        var paymentStatus = open.Count == 0
            ? "Paid"
            : (open.Any(p => PaymentService.EffectiveStatus(p) == PaymentStatuses.Overdue) ? "Overdue" : "Balance due");

        return new FamilyHomeSnapshot
        {
            CenterName = string.IsNullOrWhiteSpace(db.Settings.CenterName)
                ? "HIRAYA Learning Center"
                : db.Settings.CenterName,
            Greeting = string.IsNullOrWhiteSpace(user.FirstName) ? user.Fullname : user.FirstName,
            Children = children,
            FocusChild = focus.FirstOrDefault(),
            Today = schedule.Where(i => i.Start.Date == today).ToList(),
            Upcoming = schedule.Where(i => i.Start.Date > today).Take(6).ToList(),
            NextClass = next,
            AttendanceToday = attendance,
            AttendancePercent = percent,
            OpenPayments = open.Count,
            OverduePayments = open.Count(p => PaymentService.EffectiveStatus(p) == PaymentStatuses.Overdue),
            OpenBalance = open.Sum(PaymentService.Remaining),
            PaymentHeadline = paymentStatus,
            ProgressHeadline = recentProgress.Count == 0
                ? "No notes yet"
                : ProgressRatings.Label(recentProgress[0].Rating),
            RecentProgress = recentProgress,
            Announcements = NotificationService.PublishedNews(db).Take(3).ToList(),
            Alerts = NotificationService.Inbox(db, user).Where(a => !a.Read).Take(5).ToList()
        };
    }

    public static HashSet<string> ClassIdsForStudents(HirayaDatabase db, IEnumerable<string> studentIds)
    {
        var ids = studentIds.ToHashSet();
        return db.Enrollments
            .Where(e => ids.Contains(e.StudentId) && EnrollmentStatuses.CountsTowardCapacity(e.Status))
            .Select(e => e.ClassId)
            .ToHashSet();
    }
}
