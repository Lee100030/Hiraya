using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class ScheduleSaveRequest
{
    public string ScheduleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = ScheduleKinds.ClassSession;
    public string ClassId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public DateTime Start { get; set; } = DateTime.Today.AddHours(8);
    public DateTime End { get; set; } = DateTime.Today.AddHours(9);
    public string Room { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "confirmed";
}

public class CalendarItem
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "schedule";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = ScheduleKinds.ClassSession;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Room { get; set; } = "";
    public string Meta { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string Status { get; set; } = "";
}

public class ScheduleService
{
    private readonly IHirayaRepository _repository;

    public ScheduleService(IHirayaRepository repository) => _repository = repository;

    public static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    public static bool TryRange(string startAt, string endAt, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        if (!DateTime.TryParse(startAt, out start)) return false;
        if (!DateTime.TryParse(endAt, out end))
            end = start.AddHours(1);
        start = ToLocal(start);
        end = ToLocal(end);
        return end > start;
    }

    public static List<string> DetectConflicts(HirayaDatabase db, Schedule candidate)
    {
        var issues = new List<string>();
        if (string.Equals(candidate.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            return issues;
        if (!TryRange(candidate.StartAt, candidate.EndAt, out var start, out var end))
        {
            issues.Add("Start and end times are invalid.");
            return issues;
        }

        if (candidate.Kind is ScheduleKinds.Holiday or ScheduleKinds.Deadline or ScheduleKinds.TeacherTask)
            return issues;

        foreach (var other in db.Schedules.Where(s =>
                     s.ScheduleId != candidate.ScheduleId &&
                     !string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase)))
        {
            if (other.Kind is ScheduleKinds.Holiday or ScheduleKinds.Deadline) continue;
            if (!TryRange(other.StartAt, other.EndAt, out var otherStart, out var otherEnd)) continue;
            if (!Overlaps(start, end, otherStart, otherEnd)) continue;

            if (!string.IsNullOrWhiteSpace(candidate.TeacherId) &&
                candidate.TeacherId == other.TeacherId)
                issues.Add($"Teacher already has \"{other.Title}\" at this time.");

            if (!string.IsNullOrWhiteSpace(candidate.Room) &&
                string.Equals(candidate.Room, other.Room, StringComparison.OrdinalIgnoreCase))
                issues.Add($"Room {candidate.Room} is already used by \"{other.Title}\".");

            if (SharesStudent(db, candidate, other))
                issues.Add($"A student in this session is already in \"{other.Title}\".");
        }

        return issues.Distinct().ToList();
    }

    public async Task SaveAsync(ScheduleSaveRequest input, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageSchedule(actor?.Role))
            throw new InvalidOperationException("You cannot manage schedules.");
        var title = input.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Title is required.");
        if (input.End <= input.Start)
            throw new InvalidOperationException("End time must be after start time.");

        await _repository.MutateAsync(async db =>
        {
            var id = string.IsNullOrWhiteSpace(input.ScheduleId) ? IdFactory.New("sch") : input.ScheduleId;
            var kind = string.IsNullOrWhiteSpace(input.Kind) ? ScheduleKinds.ClassSession : input.Kind;
            var item = new Schedule
            {
                ScheduleId = id,
                Title = title,
                Kind = kind,
                ScheduleType = kind,
                ClassId = input.ClassId.Trim(),
                StudentId = input.StudentId.Trim(),
                TeacherId = input.TeacherId.Trim(),
                StartAt = DateTime.SpecifyKind(input.Start, DateTimeKind.Local).ToUniversalTime().ToString("o"),
                EndAt = DateTime.SpecifyKind(input.End, DateTimeKind.Local).ToUniversalTime().ToString("o"),
                Room = input.Room.Trim(),
                Notes = input.Notes.Trim(),
                Status = string.IsNullOrWhiteSpace(input.Status) ? "confirmed" : input.Status.Trim()
            };

            var conflicts = DetectConflicts(db, item);
            if (conflicts.Count > 0)
                throw new InvalidOperationException(string.Join(" ", conflicts));

            db.Schedules.RemoveAll(s => s.ScheduleId == id);
            db.Schedules.Add(item);
            AuditService.Append(db, actor, AuditModules.Schedule,
                string.IsNullOrWhiteSpace(input.ScheduleId) ? "create" : "update",
                "schedule", id, $"Saved schedule {title}.");
            await Task.CompletedTask;
        });
    }

    public async Task CancelAsync(string scheduleId, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageSchedule(actor?.Role))
            throw new InvalidOperationException("You cannot manage schedules.");
        await _repository.MutateAsync(async db =>
        {
            var item = db.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId)
                       ?? throw new InvalidOperationException("Schedule not found.");
            item.Status = "cancelled";
            AuditService.Append(db, actor, AuditModules.Schedule, "cancel", "schedule", scheduleId, $"Cancelled {item.Title}.");
            await Task.CompletedTask;
        });
    }

    public async Task SaveTeacherTaskAsync(ScheduleSaveRequest input, HirayaUser actor)
    {
        if (actor.Role != UserRoles.Teacher && !UserRoles.IsAdmin(actor.Role))
            throw new InvalidOperationException("Only teachers can manage personal weekly tasks.");
        var title = input.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Task title is required.");
        if (input.End <= input.Start)
            throw new InvalidOperationException("End time must be after start time.");

        await _repository.MutateAsync(async db =>
        {
            var id = string.IsNullOrWhiteSpace(input.ScheduleId) ? IdFactory.New("task") : input.ScheduleId;
            var existing = db.Schedules.FirstOrDefault(s => s.ScheduleId == id);
            if (existing != null && existing.Kind != ScheduleKinds.TeacherTask)
                throw new InvalidOperationException("That item is an official schedule, not a personal task.");
            if (actor.Role == UserRoles.Teacher &&
                existing != null &&
                existing.TeacherId != actor.UserId)
                throw new InvalidOperationException("You can only edit your own tasks.");

            var teacherId = actor.Role == UserRoles.Teacher ? actor.UserId : input.TeacherId.Trim();
            if (string.IsNullOrWhiteSpace(teacherId))
                teacherId = actor.UserId;

            var item = new Schedule
            {
                ScheduleId = id,
                Title = title,
                Kind = ScheduleKinds.TeacherTask,
                ScheduleType = ScheduleKinds.TeacherTask,
                TeacherId = teacherId,
                StartAt = DateTime.SpecifyKind(input.Start, DateTimeKind.Local).ToUniversalTime().ToString("o"),
                EndAt = DateTime.SpecifyKind(input.End, DateTimeKind.Local).ToUniversalTime().ToString("o"),
                Notes = input.Notes.Trim(),
                Status = string.IsNullOrWhiteSpace(input.Status) ? "confirmed" : input.Status.Trim()
            };
            db.Schedules.RemoveAll(s => s.ScheduleId == id);
            db.Schedules.Add(item);
            AuditService.Append(db, actor, AuditModules.Schedule,
                string.IsNullOrWhiteSpace(input.ScheduleId) ? "create" : "update",
                "task", id, $"Saved teaching task {title}.");
            await Task.CompletedTask;
        });
    }

    public async Task DeleteTeacherTaskAsync(string scheduleId, HirayaUser actor)
    {
        await _repository.MutateAsync(async db =>
        {
            var item = db.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId)
                       ?? throw new InvalidOperationException("Task not found.");
            if (item.Kind != ScheduleKinds.TeacherTask)
                throw new InvalidOperationException("Official class schedules are managed by an administrator.");
            if (actor.Role == UserRoles.Teacher && item.TeacherId != actor.UserId)
                throw new InvalidOperationException("You can only delete your own tasks.");
            if (actor.Role != UserRoles.Teacher && !UserRoles.IsAdmin(actor.Role))
                throw new InvalidOperationException("You cannot delete that task.");
            db.Schedules.RemoveAll(s => s.ScheduleId == scheduleId);
            AuditService.Append(db, actor, AuditModules.Schedule, "delete", "task", scheduleId, $"Removed teaching task {item.Title}.");
            await Task.CompletedTask;
        });
    }

    public static IEnumerable<CalendarItem> VisibleItems(HirayaDatabase db, HirayaUser user)
    {
        var items = new List<CalendarItem>();
        foreach (var schedule in db.Schedules.Where(s => !string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase)))
        {
            if (!IsVisible(db, user, schedule)) continue;
            if (!TryRange(schedule.StartAt, schedule.EndAt, out var start, out var end)) continue;
            items.Add(new CalendarItem
            {
                Id = schedule.ScheduleId,
                Source = "schedule",
                Title = schedule.Title,
                Kind = string.IsNullOrWhiteSpace(schedule.Kind) ? schedule.ScheduleType : schedule.Kind,
                Start = start,
                End = end,
                Room = schedule.Room,
                Meta = ClassLabel(db, schedule),
                StudentId = schedule.StudentId,
                ClassId = schedule.ClassId,
                Status = schedule.Status
            });
        }

        if (user.Role is UserRoles.Teacher or UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff)
        {
            foreach (var shift in db.TeacherWorkShifts.Where(s =>
                         !string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase)))
            {
                if (user.Role == UserRoles.Teacher && shift.TeacherId != user.UserId) continue;
                if (!TryShiftRange(shift, out var start, out var end)) continue;
                var teacher = db.Users.FirstOrDefault(u => u.UserId == shift.TeacherId)?.Fullname ?? "Teacher";
                items.Add(new CalendarItem
                {
                    Id = shift.ShiftId,
                    Source = "shift",
                    Title = string.IsNullOrWhiteSpace(shift.Title) ? "Duty" : shift.Title,
                    Kind = "duty",
                    Start = start,
                    End = end,
                    Room = shift.Room,
                    Meta = teacher,
                    Status = shift.Status
                });
            }
        }

        foreach (var payment in PaymentService.Visible(db, user)
                     .Where(p => PaymentStatuses.IsOpen(PaymentService.EffectiveStatus(p))))
        {
            var due = PaymentService.DueLocal(payment);
            if (due == null) continue;
            var day = due.Value.Date;
            items.Add(new CalendarItem
            {
                Id = payment.PaymentId,
                Source = "payment",
                Title = $"Payment {PaymentService.EffectiveStatus(payment)} · ₱{PaymentService.Remaining(payment):N0}",
                Kind = ScheduleKinds.Deadline,
                Start = day,
                End = day.AddHours(23),
                Meta = payment.EnrollmentId,
                StudentId = PaymentService.ResolveStudentId(db, payment),
                Status = PaymentService.EffectiveStatus(payment)
            });
        }

        return items;
    }

    private static bool IsVisible(HirayaDatabase db, HirayaUser user, Schedule schedule)
    {
        if (schedule.Kind == ScheduleKinds.TeacherTask)
        {
            if (user.Role is UserRoles.SuperAdmin or UserRoles.Admin)
                return true;
            return user.Role == UserRoles.Teacher && schedule.TeacherId == user.UserId;
        }

        if (user.Role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff)
            return true;
        if (user.Role == UserRoles.Teacher)
            return schedule.TeacherId == user.UserId;
        if (user.Role == UserRoles.Parent || user.Role == UserRoles.Student)
        {
            var kids = FamilyPortal.StudentIds(db, user);
            if (kids.Contains(schedule.StudentId)) return true;
            var classIds = db.Enrollments
                .Where(e => kids.Contains(e.StudentId) && EnrollmentStatuses.CountsTowardCapacity(e.Status))
                .Select(e => e.ClassId)
                .ToHashSet();
            return classIds.Contains(schedule.ClassId);
        }

        return false;
    }

    private static bool SharesStudent(HirayaDatabase db, Schedule a, Schedule b)
    {
        var aStudents = StudentIds(db, a);
        var bStudents = StudentIds(db, b);
        return aStudents.Overlaps(bStudents);
    }

    private static HashSet<string> StudentIds(HirayaDatabase db, Schedule schedule)
    {
        var ids = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(schedule.StudentId))
            ids.Add(schedule.StudentId);
        if (!string.IsNullOrWhiteSpace(schedule.ClassId))
        {
            foreach (var enrollment in db.Enrollments.Where(e =>
                         e.ClassId == schedule.ClassId && EnrollmentStatuses.CountsTowardCapacity(e.Status)))
                ids.Add(enrollment.StudentId);
        }

        return ids;
    }

    private static string ClassLabel(HirayaDatabase db, Schedule schedule)
    {
        if (!string.IsNullOrWhiteSpace(schedule.ClassId))
            return db.Classes.FirstOrDefault(c => c.ClassId == schedule.ClassId)?.Name ?? schedule.ClassId;
        if (!string.IsNullOrWhiteSpace(schedule.StudentId))
        {
            var student = db.Students.FirstOrDefault(s => s.StudentId == schedule.StudentId);
            return student == null ? "" : StudentNames.Display(student);
        }

        return ScheduleKinds.Label(schedule.Kind);
    }

    private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;

    private static DateTime ToLocal(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : DateTime.SpecifyKind(value, DateTimeKind.Local);

    private static bool TryShiftRange(TeacherWorkShift shift, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        if (!DateTime.TryParse(shift.WorkDate, out var day)) return false;
        if (!TimeSpan.TryParse(shift.StartTime, out var startTime) || !TimeSpan.TryParse(shift.EndTime, out var endTime))
            return false;
        start = day.Date + startTime;
        end = day.Date + endTime;
        return end > start;
    }
}
