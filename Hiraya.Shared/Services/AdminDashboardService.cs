using Hiraya.Shared.Models;

namespace Hiraya.Shared.Services;

public class AdminDashboardSnapshot
{
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int NewStudents { get; set; }
    public int InactiveStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int ActiveTeachers { get; set; }
    public int TeacherPresentToday { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }
    public int ExcusedToday { get; set; }
    public int PendingEnrollments { get; set; }
    public int ActiveEnrollments { get; set; }
    public List<string> RecentlyEnrolled { get; set; } = [];
    public int ActiveClasses { get; set; }
    public int ClassesToday { get; set; }
    public int UpcomingClasses { get; set; }
    public int TotalPayments { get; set; }
    public int PaymentsToday { get; set; }
    public int PendingPayments { get; set; }
    public int OverduePayments { get; set; }
    public int RecentAssessments { get; set; }
    public int NeedsAttention { get; set; }
    public List<string> RecentProgress { get; set; } = [];
    public List<AuditLogEntry> RecentActivity { get; set; } = [];
    public List<string> Alerts { get; set; } = [];
}

public static class AdminDashboardService
{
    public static AdminDashboardSnapshot Build(HirayaDatabase db)
    {
        var today = DateTime.UtcNow.Date;
        var todayKey = today.ToString("yyyy-MM-dd");
        var weekAgo = today.AddDays(-14);

        var students = db.Students;
        var teachers = db.Users.Where(u => u.Role == UserRoles.Teacher).ToList();
        var studentToday = db.Attendance.Where(a =>
            a.Kind != AttendanceKinds.Employee &&
            AttendanceService.DateKey(a.AttendanceDate) == todayKey);
        var teacherToday = db.Attendance.Where(a =>
            a.Kind == AttendanceKinds.Employee &&
            AttendanceService.DateKey(a.AttendanceDate) == todayKey);

        var recentEnroll = db.Enrollments
            .Where(e => DateTime.TryParse(e.EnrollmentDate, out var d) && d.Date >= weekAgo)
            .OrderByDescending(e => e.EnrollmentDate)
            .Take(5)
            .Select(e =>
            {
                var name = StudentNames.Display(students.FirstOrDefault(s => s.StudentId == e.StudentId) ?? new Student { Fullname = e.StudentId });
                return $"{name} · {e.Status}";
            })
            .ToList();

        var recentProgress = db.Reports
            .OrderByDescending(r => r.UpdatedAt)
            .Take(5)
            .Select(r =>
            {
                var name = StudentNames.Display(students.FirstOrDefault(s => s.StudentId == r.StudentId) ?? new Student { Fullname = r.StudentId });
                return $"{name} · {ProgressAreas.Label(r.Area)} · {ProgressRatings.Label(r.Rating)}";
            })
            .ToList();

        var alerts = new List<string>();
        var pendingEnroll = db.Enrollments.Count(e => e.Status is "pending" or "pending_review")
                            + db.EnrollmentApplications.Count(a => a.Status is "pending" or "pending_review");
        if (pendingEnroll > 0)
            alerts.Add($"{pendingEnroll} enrollment item(s) need approval.");
        var absent = studentToday.Count(a => a.Status == AttendanceStatuses.Absent);
        if (absent > 0)
            alerts.Add($"{absent} student(s) are absent today.");
        var overdue = db.Payments.Count(p => PaymentService.EffectiveStatus(p) == PaymentStatuses.Overdue);
        if (overdue > 0)
            alerts.Add($"{overdue} payment(s) are overdue.");
        var conflicts = CountScheduleConflicts(db);
        if (conflicts > 0)
            alerts.Add($"{conflicts} teacher or room schedule conflict(s) detected.");
        if (db.EnrollmentApplications.Any(a => a.Status is "pending" or "pending_review"))
            alerts.Add("A new student enrollment was submitted.");

        return new AdminDashboardSnapshot
        {
            TotalStudents = students.Count,
            ActiveStudents = students.Count(s => IsActive(s.Status)),
            NewStudents = students.Count(s => DateTime.TryParse(s.EnrollmentDate, out var d) && d.Date >= weekAgo),
            InactiveStudents = students.Count(s => !IsActive(s.Status)),
            TotalTeachers = teachers.Count,
            ActiveTeachers = teachers.Count(t => IsActive(t.Status)),
            TeacherPresentToday = teacherToday.Count(a => AttendanceStatuses.IsPresentLike(a.Status)),
            PresentToday = studentToday.Count(a => a.Status == AttendanceStatuses.Present || string.IsNullOrEmpty(a.Status)),
            AbsentToday = absent,
            LateToday = studentToday.Count(a => a.Status == AttendanceStatuses.Late),
            ExcusedToday = studentToday.Count(a => a.Status == AttendanceStatuses.Excused),
            PendingEnrollments = pendingEnroll,
            ActiveEnrollments = db.Enrollments.Count(e => e.Status == EnrollmentStatuses.Active),
            RecentlyEnrolled = recentEnroll,
            ActiveClasses = db.Classes.Count(c => IsActive(c.Status)),
            ClassesToday = db.Schedules.Count(s => ScheduleFallsOn(s, today)),
            UpcomingClasses = db.Schedules.Count(s =>
                DateTime.TryParse(s.StartAt, out var start) && start.Date > today && start.Date <= today.AddDays(7)),
            TotalPayments = db.Payments.Count,
            PaymentsToday = db.Payments.Count(p =>
                DateTime.TryParse(string.IsNullOrWhiteSpace(p.PaidAt) ? p.PaymentDate : p.PaidAt, out var d) &&
                d.Date == today),
            PendingPayments = db.Payments.Count(p => PaymentStatuses.IsOpen(PaymentService.EffectiveStatus(p))),
            OverduePayments = overdue,
            RecentAssessments = db.Reports.Count(r =>
                DateTime.TryParse(r.UpdatedAt, out var d) && d.Date >= weekAgo),
            NeedsAttention = db.Reports.Count(r =>
                r.Status != ProgressStatuses.Archived && r.Rating == ProgressRatings.NeedsSupport),
            RecentProgress = recentProgress,
            RecentActivity = db.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(8).ToList(),
            Alerts = alerts
        };
    }

    private static bool IsActive(string? status) =>
        string.IsNullOrEmpty(status) || string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);

    private static bool ScheduleFallsOn(Schedule schedule, DateTime day)
    {
        if (!DateTime.TryParse(schedule.StartAt, out var start))
            return false;
        if (!DateTime.TryParse(schedule.EndAt, out var end))
            end = start;
        return day >= start.Date && day <= end.Date;
    }

    private static int CountScheduleConflicts(HirayaDatabase db)
    {
        var confirmed = db.Schedules
            .Where(s => !string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var count = 0;
        for (var i = 0; i < confirmed.Count; i++)
        {
            for (var j = i + 1; j < confirmed.Count; j++)
            {
                var a = confirmed[i];
                var b = confirmed[j];
                if (!Overlaps(a, b))
                    continue;
                var teacher = !string.IsNullOrWhiteSpace(a.TeacherId) && a.TeacherId == b.TeacherId;
                var room = !string.IsNullOrWhiteSpace(a.Room) &&
                           string.Equals(a.Room, b.Room, StringComparison.OrdinalIgnoreCase);
                if (teacher || room)
                    count++;
            }
        }
        return count;
    }

    private static bool Overlaps(Schedule a, Schedule b)
    {
        if (!DateTime.TryParse(a.StartAt, out var as_) || !DateTime.TryParse(b.StartAt, out var bs))
            return false;
        var ae = DateTime.TryParse(a.EndAt, out var aEnd) ? aEnd : as_.AddHours(1);
        var be = DateTime.TryParse(b.EndAt, out var bEnd) ? bEnd : bs.AddHours(1);
        return as_ < be && bs < ae;
    }
}
