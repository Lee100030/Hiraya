using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class AttendanceService
{
    private readonly IHirayaRepository _repository;

    public AttendanceService(IHirayaRepository repository) => _repository = repository;

    public static string DateKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        return DateTime.TryParse(value, out var date)
            ? date.ToString("yyyy-MM-dd")
            : value.Length >= 10 ? value[..10] : value;
    }

    public static AttendanceRecord? FindStudent(HirayaDatabase db, string studentId, string classId, string date) =>
        db.Attendance.FirstOrDefault(a =>
            a.Kind != AttendanceKinds.Employee &&
            a.StudentId == studentId &&
            string.Equals(a.ClassId ?? "", classId ?? "", StringComparison.Ordinal) &&
            DateKey(a.AttendanceDate) == date);

    public static AttendanceRecord? FindEmployee(HirayaDatabase db, string employeeId, string date) =>
        db.Attendance.FirstOrDefault(a =>
            a.Kind == AttendanceKinds.Employee &&
            a.EmployeeId == employeeId &&
            DateKey(a.AttendanceDate) == date);

    public async Task MarkStudentAsync(string studentId, string classId, string date, string status, string notes, string recordedBy)
    {
        if (!AttendanceStatuses.ForStudents.Contains(status))
            throw new InvalidOperationException("Invalid student attendance status.");

        await _repository.MutateAsync(async db =>
        {
            UpsertStudent(db, studentId, classId, DateKey(date), status, notes, recordedBy, scan: false);
            var student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            AuditService.Append(db, recordedBy, student?.Fullname ?? recordedBy, AuditModules.Attendance, "mark",
                "attendance", studentId, $"Marked {StudentNames.Display(student ?? new Student { Fullname = studentId })} {status}.");
            await Task.CompletedTask;
        });
    }

    public async Task MarkClassRosterAsync(string classId, string date, IReadOnlyList<(string StudentId, string Status)> marks, string recordedBy)
    {
        var day = DateKey(date);
        await _repository.MutateAsync(async db =>
        {
            foreach (var mark in marks)
            {
                if (!AttendanceStatuses.ForStudents.Contains(mark.Status))
                    throw new InvalidOperationException($"Invalid status for a student: {mark.Status}");
                UpsertStudent(db, mark.StudentId, classId, day, mark.Status, "", recordedBy, scan: false);
            }
            var cls = db.Classes.FirstOrDefault(c => c.ClassId == classId);
            AuditService.Append(db, recordedBy, "", AuditModules.Attendance, "roster",
                "class", classId, $"Saved class attendance for {cls?.Name ?? classId} ({marks.Count} students).");
            await Task.CompletedTask;
        });
    }

    public async Task MarkEmployeeAsync(string employeeId, string date, string status, string notes, string recordedBy)
    {
        if (!AttendanceStatuses.ForEmployees.Contains(status))
            throw new InvalidOperationException("Invalid staff attendance status.");

        await _repository.MutateAsync(async db =>
        {
            var user = db.Users.FirstOrDefault(u => u.UserId == employeeId && Navigation.IsEmployeeRole(u.Role))
                       ?? throw new InvalidOperationException("Teacher or staff account not found.");
            var day = DateKey(date);
            var existing = FindEmployee(db, employeeId, day);
            if (existing == null)
            {
                db.Attendance.Add(new AttendanceRecord
                {
                    AttendanceId = IdFactory.New("att"),
                    Kind = AttendanceKinds.Employee,
                    EmployeeId = employeeId,
                    AttendanceDate = day,
                    Status = status,
                    Notes = notes.Trim(),
                    TimeIn = AttendanceStatuses.IsPresentLike(status) ? DateTime.UtcNow.ToString("o") : "",
                    RecordedBy = recordedBy
                });
            }
            else
            {
                existing.Status = status;
                existing.Notes = notes.Trim();
                existing.RecordedBy = recordedBy;
                if (AttendanceStatuses.IsPresentLike(status) && string.IsNullOrEmpty(existing.TimeIn))
                    existing.TimeIn = DateTime.UtcNow.ToString("o");
            }

            _ = user;
            AuditService.Append(db, recordedBy, user.Fullname, AuditModules.Attendance, "staff",
                "user", employeeId, $"Marked {user.Fullname} {status}.");
            await Task.CompletedTask;
        });
    }

    public async Task<(bool Ok, string Message)> ScanStudentAsync(string qrPayload, string? classId)
    {
        string message = "";
        var ok = false;

        await _repository.MutateAsync(async db =>
        {
            var student = db.Students.FirstOrDefault(s =>
                string.Equals(s.QrCode, qrPayload, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.StudentId, qrPayload, StringComparison.OrdinalIgnoreCase));

            if (student == null)
            {
                message = "Student not found for that QR code.";
                return;
            }

            var resolvedClass = ResolveClassId(db, student.StudentId, classId);
            if (string.IsNullOrWhiteSpace(resolvedClass))
            {
                message = $"{StudentNames.Display(student)} is not in an active class. Assign a class first.";
                return;
            }

            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            var existing = FindStudent(db, student.StudentId, resolvedClass, today);

            if (existing == null)
            {
                UpsertStudent(db, student.StudentId, resolvedClass, today, AttendanceStatuses.Present, "QR scan check-in", "scan", scan: true);
                NotifyParent(db, student, $"{StudentNames.Display(student)} checked in.");
                message = $"Checked in {StudentNames.Display(student)} ({ClassName(db, resolvedClass)}).";
                ok = true;
                AuditService.Append(db, "scan", "QR scan", AuditModules.Attendance, "check-in",
                    "student", student.StudentId, message);
            }
            else if (string.IsNullOrEmpty(existing.TimeOut) && AttendanceStatuses.IsPresentLike(existing.Status))
            {
                existing.TimeOut = DateTime.UtcNow.ToString("o");
                NotifyParent(db, student, $"{StudentNames.Display(student)} checked out.");
                message = $"Checked out {StudentNames.Display(student)}.";
                ok = true;
                AuditService.Append(db, "scan", "QR scan", AuditModules.Attendance, "check-out",
                    "student", student.StudentId, message);
            }
            else
            {
                message = $"{StudentNames.Display(student)} already has {existing.Status} attendance today for that class. An admin or teacher can edit the record.";
            }

            await Task.CompletedTask;
        });

        return (ok, message);
    }

    private static void UpsertStudent(
        HirayaDatabase db,
        string studentId,
        string classId,
        string date,
        string status,
        string notes,
        string recordedBy,
        bool scan)
    {
        var student = db.Students.FirstOrDefault(s => s.StudentId == studentId)
                      ?? throw new InvalidOperationException("Student not found.");

        var existing = FindStudent(db, studentId, classId, date);
        if (existing == null)
        {
            db.Attendance.Add(new AttendanceRecord
            {
                AttendanceId = IdFactory.New("att"),
                Kind = AttendanceKinds.Student,
                StudentId = studentId,
                ClassId = classId,
                QrCode = student.QrCode,
                AttendanceDate = date,
                Status = status,
                Notes = notes,
                TimeIn = AttendanceStatuses.IsPresentLike(status) || scan ? DateTime.UtcNow.ToString("o") : "",
                RecordedBy = recordedBy
            });
            return;
        }

        existing.Status = status;
        existing.ClassId = classId;
        existing.Kind = AttendanceKinds.Student;
        existing.Notes = string.IsNullOrWhiteSpace(notes) ? existing.Notes : notes;
        existing.RecordedBy = recordedBy;
        if (AttendanceStatuses.IsPresentLike(status) && string.IsNullOrEmpty(existing.TimeIn))
            existing.TimeIn = DateTime.UtcNow.ToString("o");
        if (status is AttendanceStatuses.Absent or AttendanceStatuses.Excused)
            existing.TimeOut = null;
    }

    private static string ResolveClassId(HirayaDatabase db, string studentId, string? classId)
    {
        if (!string.IsNullOrWhiteSpace(classId))
            return classId.Trim();

        var active = db.Enrollments.FirstOrDefault(e =>
            e.StudentId == studentId &&
            !string.IsNullOrWhiteSpace(e.ClassId) &&
            EnrollmentStatuses.CountsTowardCapacity(e.Status));
        return active?.ClassId ?? "";
    }

    private static string ClassName(HirayaDatabase db, string classId) =>
        db.Classes.FirstOrDefault(c => c.ClassId == classId)?.Name ?? classId;

    private static void NotifyParent(HirayaDatabase db, Student student, string message)
    {
        if (string.IsNullOrEmpty(student.ParentId)) return;
        NotificationService.Push(
            db,
            student.ParentId,
            message,
            NotificationKinds.Attendance,
            "Attendance",
            "attendance");
    }
}
