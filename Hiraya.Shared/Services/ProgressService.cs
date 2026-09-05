using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class ProgressSaveRequest
{
    public string ReportId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string Period { get; set; } = "";
    public string Area { get; set; } = ProgressAreas.Overall;
    public string Rating { get; set; } = ProgressRatings.Developing;
    public string Evaluation { get; set; } = "";
    public string BehaviorReport { get; set; } = "";
    public string DevelopmentalReport { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string Status { get; set; } = ProgressStatuses.Draft;
}

public class ProgressService
{
    private readonly IHirayaRepository _repository;

    public ProgressService(IHirayaRepository repository) => _repository = repository;

    public static IEnumerable<ProgressReport> Visible(HirayaDatabase db, HirayaUser user)
    {
        IEnumerable<ProgressReport> reports = db.Reports;

        if (user.Role is UserRoles.Parent or UserRoles.Student)
        {
            var kids = FamilyPortal.StudentIds(db, user);
            reports = reports.Where(r =>
                kids.Contains(r.StudentId) &&
                r.Status == ProgressStatuses.Published);
        }
        else if (user.Role == UserRoles.Teacher)
        {
            var allowed = RosterStudentIds(db, user.UserId);
            reports = reports.Where(r =>
                r.TeacherId == user.UserId ||
                allowed.Contains(r.StudentId));
        }

        if (!Navigation.CanWriteProgress(user.Role) && user.Role != UserRoles.Staff)
            reports = reports.Where(r => r.Status != ProgressStatuses.Archived);
        else if (user.Role == UserRoles.Staff)
            reports = reports.Where(r => r.Status != ProgressStatuses.Archived);

        return reports;
    }

    public static IEnumerable<Student> WritableStudents(HirayaDatabase db, HirayaUser user)
    {
        var students = db.Students.Where(s => !string.Equals(s.Status, "archived", StringComparison.OrdinalIgnoreCase));
        if (user.Role == UserRoles.Teacher)
        {
            var allowed = RosterStudentIds(db, user.UserId);
            students = students.Where(s => allowed.Contains(s.StudentId));
        }

        return students.OrderBy(StudentNames.Display);
    }

    public static IEnumerable<LearningClass> WritableClasses(HirayaDatabase db, HirayaUser user)
    {
        var classes = db.Classes.Where(c => !string.Equals(c.Status, "archived", StringComparison.OrdinalIgnoreCase));
        if (user.Role == UserRoles.Teacher)
            classes = classes.Where(c => c.TeacherId == user.UserId);
        return classes.OrderBy(c => c.Name);
    }

    public static IReadOnlyList<ProgressReport> Timeline(HirayaDatabase db, HirayaUser user, string studentId) =>
        Visible(db, user)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToList();

    public async Task SaveAsync(ProgressSaveRequest input, HirayaUser actor)
    {
        if (!Navigation.CanWriteProgress(actor.Role))
            throw new InvalidOperationException("You cannot write progress records.");
        if (string.IsNullOrWhiteSpace(input.StudentId))
            throw new InvalidOperationException("Select a student.");
        if (string.IsNullOrWhiteSpace(input.Evaluation))
            throw new InvalidOperationException("Evaluation notes are required.");
        if (!ProgressAreas.All.Contains(input.Area))
            throw new InvalidOperationException("Select a learning area.");
        if (!ProgressRatings.All.Contains(input.Rating))
            throw new InvalidOperationException("Select a rating.");
        if (input.Status is not (ProgressStatuses.Draft or ProgressStatuses.Published))
            throw new InvalidOperationException("Save as draft or published.");

        await _repository.MutateAsync(async db =>
        {
            EnsureCanWriteStudent(db, actor, input.StudentId, input.ClassId);

            var student = db.Students.FirstOrDefault(s => s.StudentId == input.StudentId)
                          ?? throw new InvalidOperationException("Student not found.");

            var classId = input.ClassId.Trim();
            if (string.IsNullOrWhiteSpace(classId))
            {
                classId = db.Enrollments
                    .Where(e => e.StudentId == input.StudentId && EnrollmentStatuses.CountsTowardCapacity(e.Status))
                    .Select(e => e.ClassId)
                    .FirstOrDefault() ?? "";
            }

            if (!string.IsNullOrWhiteSpace(classId) && db.Classes.All(c => c.ClassId != classId))
                throw new InvalidOperationException("Class was not found.");

            var teacherId = actor.Role == UserRoles.Teacher
                ? actor.UserId
                : db.Classes.FirstOrDefault(c => c.ClassId == classId)?.TeacherId
                  ?? db.Enrollments.FirstOrDefault(e => e.StudentId == input.StudentId && e.ClassId == classId)?.TeacherId
                  ?? actor.UserId;

            var now = DateTime.UtcNow.ToString("o");
            var isNew = string.IsNullOrWhiteSpace(input.ReportId);
            var reportId = isNew ? IdFactory.New("rep") : input.ReportId;
            var existing = db.Reports.FirstOrDefault(r => r.ReportId == reportId);

            if (!isNew && existing == null)
                throw new InvalidOperationException("Progress record not found.");
            if (existing != null && existing.Status == ProgressStatuses.Archived)
                throw new InvalidOperationException("Archived records cannot be edited. Create a new entry instead.");

            var next = new ProgressReport
            {
                ReportId = reportId,
                StudentId = input.StudentId,
                TeacherId = existing?.TeacherId is { Length: > 0 } priorTeacher ? priorTeacher : teacherId,
                ClassId = classId,
                Period = input.Period.Trim(),
                Area = input.Area,
                Rating = input.Rating,
                Evaluation = input.Evaluation.Trim(),
                BehaviorReport = input.BehaviorReport.Trim(),
                DevelopmentalReport = input.DevelopmentalReport.Trim(),
                Remarks = input.Remarks.Trim(),
                UploadedMedia = existing?.UploadedMedia ?? "",
                Status = input.Status,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now,
                History = (existing?.History ?? []).ToList()
            };

            var action = isNew ? "created" : existing!.Status != next.Status ? next.Status : "updated";
            next.History.Add(new ProgressHistoryEntry
            {
                EntryId = IdFactory.New("ph"),
                ChangedBy = actor.UserId,
                ChangedAt = now,
                Action = action,
                Summary = isNew ? "Record created." : DescribeChanges(existing!, next)
            });

            db.Reports.RemoveAll(r => r.ReportId == reportId);
            db.Reports.Add(next);

            if (next.Status == ProgressStatuses.Published && (isNew || existing?.Status != ProgressStatuses.Published))
                NotifyParent(db, student, next);

            AuditService.Append(db, actor, AuditModules.Progress, isNew ? "create" : "update",
                "progress", reportId, $"{(isNew ? "Created" : "Updated")} {ProgressAreas.Label(next.Area)} progress for {StudentNames.Display(student)} ({next.Status}).");
            await Task.CompletedTask;
        });
    }

    public async Task PublishAsync(string reportId, HirayaUser actor) =>
        await SetStatusAsync(reportId, actor, ProgressStatuses.Published);

    public async Task ArchiveAsync(string reportId, HirayaUser actor) =>
        await SetStatusAsync(reportId, actor, ProgressStatuses.Archived);

    private async Task SetStatusAsync(string reportId, HirayaUser actor, string status)
    {
        if (!Navigation.CanWriteProgress(actor.Role))
            throw new InvalidOperationException("You cannot change progress records.");

        await _repository.MutateAsync(async db =>
        {
            var report = db.Reports.FirstOrDefault(r => r.ReportId == reportId)
                         ?? throw new InvalidOperationException("Progress record not found.");
            EnsureCanWriteStudent(db, actor, report.StudentId, report.ClassId);

            if (report.Status == status)
                return;

            var previous = report.Status;
            report.Status = status;
            report.UpdatedAt = DateTime.UtcNow.ToString("o");
            report.History.Add(new ProgressHistoryEntry
            {
                EntryId = IdFactory.New("ph"),
                ChangedBy = actor.UserId,
                ChangedAt = report.UpdatedAt,
                Action = status,
                Summary = $"Status changed from {previous} to {status}."
            });

            if (status == ProgressStatuses.Published && previous != ProgressStatuses.Published)
            {
                var student = db.Students.FirstOrDefault(s => s.StudentId == report.StudentId);
                if (student != null)
                    NotifyParent(db, student, report);
            }

            AuditService.Append(db, actor, AuditModules.Progress, status,
                "progress", reportId, $"Progress record {status}.");
            await Task.CompletedTask;
        });
    }

    public static HashSet<string> AssignedStudentIds(HirayaDatabase db, string teacherId) =>
        db.Enrollments
            .Where(e =>
                (e.TeacherId == teacherId ||
                 db.Classes.Any(c => c.ClassId == e.ClassId && c.TeacherId == teacherId)) &&
                EnrollmentStatuses.CountsTowardCapacity(e.Status))
            .Select(e => e.StudentId)
            .ToHashSet();

    private static HashSet<string> RosterStudentIds(HirayaDatabase db, string teacherId) =>
        AssignedStudentIds(db, teacherId);

    private static void EnsureCanWriteStudent(HirayaDatabase db, HirayaUser actor, string studentId, string classId)
    {
        if (actor.Role is UserRoles.SuperAdmin or UserRoles.Admin)
            return;

        var allowed = RosterStudentIds(db, actor.UserId);
        if (!allowed.Contains(studentId))
            throw new InvalidOperationException("You can only record progress for students in your classes.");

        if (!string.IsNullOrWhiteSpace(classId))
        {
            var cls = db.Classes.FirstOrDefault(c => c.ClassId == classId)
                      ?? throw new InvalidOperationException("Class was not found.");
            if (cls.TeacherId != actor.UserId)
                throw new InvalidOperationException("That class is not assigned to you.");
        }
    }

    private static void NotifyParent(HirayaDatabase db, Student student, ProgressReport report)
    {
        if (string.IsNullOrWhiteSpace(student.ParentId))
            return;

        NotificationService.Push(
            db,
            student.ParentId,
            $"New {ProgressAreas.Label(report.Area).ToLowerInvariant()} progress update for {StudentNames.Display(student)}.",
            NotificationKinds.Progress,
            "Progress update",
            "progress");
    }

    private static string DescribeChanges(ProgressReport before, ProgressReport after)
    {
        var parts = new List<string>();
        if (before.Area != after.Area) parts.Add($"area → {ProgressAreas.Label(after.Area)}");
        if (before.Rating != after.Rating) parts.Add($"rating → {ProgressRatings.Label(after.Rating)}");
        if (before.Period != after.Period) parts.Add($"period → {after.Period}");
        if (before.ClassId != after.ClassId) parts.Add("class changed");
        if (before.Evaluation != after.Evaluation) parts.Add("evaluation updated");
        if (before.BehaviorReport != after.BehaviorReport) parts.Add("behavior notes updated");
        if (before.DevelopmentalReport != after.DevelopmentalReport) parts.Add("developmental notes updated");
        if (before.Remarks != after.Remarks) parts.Add("remarks updated");
        if (before.Status != after.Status) parts.Add($"status → {after.Status}");
        return parts.Count == 0 ? "Saved with no field changes." : string.Join("; ", parts);
    }
}
