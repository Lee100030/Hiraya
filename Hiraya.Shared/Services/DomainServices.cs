using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class EnrollmentService
{
    private readonly IHirayaRepository _repository;

    public EnrollmentService(IHirayaRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> SubmitPublicApplicationAsync(
        string studentFullname,
        string studentBirthdate,
        string studentGender,
        string preferredProgram,
        string parentFullname,
        string parentEmail,
        string parentPhone)
    {
        if (string.IsNullOrWhiteSpace(studentFullname) || studentFullname.Trim().Length < 2)
            throw new InvalidOperationException("Please enter the child’s full name.");
        if (!DateTime.TryParse(studentBirthdate, out _))
            throw new InvalidOperationException("Please enter a valid date of birth.");
        if (string.IsNullOrWhiteSpace(parentFullname) || parentFullname.Trim().Length < 2)
            throw new InvalidOperationException("Please enter the parent or guardian’s name.");
        if (string.IsNullOrWhiteSpace(parentEmail) || !parentEmail.Contains('@'))
            throw new InvalidOperationException("Please enter a valid email address.");
        var digits = new string((parentPhone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 7)
            throw new InvalidOperationException("Please enter a valid contact number.");
        if (string.IsNullOrWhiteSpace(preferredProgram))
            throw new InvalidOperationException("Please choose a program.");

        var age = ComputeAge(studentBirthdate);
        var applicationId = $"app_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        var childName = studentFullname.Trim();
        var guardianName = parentFullname.Trim();
        var guardianEmail = parentEmail!.Trim();
        var guardianPhone = (parentPhone ?? "").Trim();
        var program = preferredProgram.Trim();

        await _repository.MutateAsync(async db =>
        {
            db.EnrollmentApplications.Add(new EnrollmentApplication
            {
                ApplicationId = applicationId,
                StudentFullname = childName,
                StudentBirthdate = studentBirthdate,
                StudentAge = age,
                StudentGender = studentGender,
                ParentFullname = guardianName,
                ParentEmail = guardianEmail,
                ParentPhone = guardianPhone,
                PreferredProgram = program,
                SubmittedAt = DateTime.UtcNow.ToString("o"),
                Status = "pending_review"
            });

            NotificationService.PushToUsers(
                db,
                NotificationService.Recipients(db, NotificationAudiences.Admins).Select(u => u.UserId),
                $"New enrollment application from {guardianName} for {childName}.",
                NotificationKinds.Enrollment,
                "New enrollment application",
                "enrollment");

            AuditService.Append(db, "", "Public", AuditModules.Enrollment, "apply",
                "application", applicationId, $"Application submitted for {childName}.");
            await Task.CompletedTask;
        });

        return applicationId;
    }

    public async Task ApproveApplicationAsync(string applicationId, string adminUserId, string? classId = null)
    {
        await _repository.MutateAsync(async db =>
        {
            var app = db.EnrollmentApplications.FirstOrDefault(a => a.ApplicationId == applicationId)
                      ?? throw new InvalidOperationException("Application not found.");

            if (app.Status != "pending_review")
                throw new InvalidOperationException("Application already processed.");

            var learningClass = ResolveClass(db, classId, app.PreferredProgram);
            if (learningClass != null && ClassService.OccupiedSeats(db, learningClass.ClassId) >= learningClass.Capacity)
                throw new InvalidOperationException($"Class \"{learningClass.Name}\" is at capacity.");

            var studentNumber = $"STU-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            var studentId = $"stu_{Guid.NewGuid():N}"[..16];
            var parent = db.Users.FirstOrDefault(u =>
                u.Role == UserRoles.Parent &&
                string.Equals(u.Email, app.ParentEmail, StringComparison.OrdinalIgnoreCase));

            var programName = learningClass == null
                ? app.PreferredProgram
                : ClassService.ProgramName(db, learningClass.ProgramId);

            db.Students.Add(new Student
            {
                StudentId = studentId,
                Fullname = app.StudentFullname,
                Birthdate = app.StudentBirthdate,
                Age = app.StudentAge,
                Gender = app.StudentGender,
                ParentId = parent?.UserId ?? "",
                QrCode = $"HIRAYA-{studentNumber}",
                GradeLevel = programName,
                Program = programName,
                Status = "active",
                EnrollmentDate = DateTime.UtcNow.ToString("o"),
                Notes = $"Enrolled via application {applicationId}"
            });

            db.Enrollments.Add(new Enrollment
            {
                EnrollmentId = $"enr_{Guid.NewGuid():N}"[..16],
                StudentId = studentId,
                ClassId = learningClass?.ClassId ?? "",
                ProgramId = learningClass?.ProgramId ?? "",
                TeacherId = learningClass?.TeacherId ?? "",
                Period = DefaultPeriod(),
                Status = EnrollmentStatuses.Active,
                EnrollmentDate = DateTime.UtcNow.ToString("o"),
                ApprovedBy = adminUserId
            });

            app.Status = "approved";
            app.StudentNumber = studentNumber;
            AuditService.Append(db, adminUserId, "", AuditModules.Enrollment, "approve",
                "application", applicationId, $"Approved application for {app.StudentFullname}.");
            await Task.CompletedTask;
        });
    }

    public async Task RejectApplicationAsync(string applicationId)
    {
        await _repository.MutateAsync(async db =>
        {
            var app = db.EnrollmentApplications.FirstOrDefault(a => a.ApplicationId == applicationId)
                      ?? throw new InvalidOperationException("Application not found.");
            app.Status = "rejected";
            AuditService.Append(db, "", "", AuditModules.Enrollment, "reject",
                "application", applicationId, $"Rejected application for {app.StudentFullname}.");
            await Task.CompletedTask;
        });
    }

    public async Task EnrollStudentAsync(string studentId, string classId, string period, string adminUserId, string status)
    {
        await _repository.MutateAsync(async db =>
        {
            var student = db.Students.FirstOrDefault(s => s.StudentId == studentId)
                          ?? throw new InvalidOperationException("Student not found.");
            var learningClass = db.Classes.FirstOrDefault(c => c.ClassId == classId)
                                ?? throw new InvalidOperationException("Class not found.");
            if (string.Equals(learningClass.Status, "archived", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot enroll into an archived class.");

            var normalized = string.IsNullOrWhiteSpace(status) ? EnrollmentStatuses.Pending : status.Trim();
            if (EnrollmentStatuses.CountsTowardCapacity(normalized) &&
                ClassService.OccupiedSeats(db, classId) >= learningClass.Capacity)
                throw new InvalidOperationException($"Class \"{learningClass.Name}\" is at capacity.");

            if (db.Enrollments.Any(e =>
                    e.StudentId == studentId &&
                    e.ClassId == classId &&
                    EnrollmentStatuses.CountsTowardCapacity(e.Status)))
                throw new InvalidOperationException("That student is already in this class.");

            var programName = ClassService.ProgramName(db, learningClass.ProgramId);
            db.Enrollments.Add(new Enrollment
            {
                EnrollmentId = IdFactory.New("enr"),
                StudentId = studentId,
                ClassId = classId,
                ProgramId = learningClass.ProgramId,
                TeacherId = learningClass.TeacherId,
                Period = string.IsNullOrWhiteSpace(period) ? DefaultPeriod() : period.Trim(),
                Status = normalized,
                EnrollmentDate = DateTime.UtcNow.ToString("o"),
                ApprovedBy = normalized == EnrollmentStatuses.Active ? adminUserId : ""
            });

            student.Program = programName;
            student.GradeLevel = programName;
            if (normalized == EnrollmentStatuses.Active)
                student.Status = "active";

            AuditService.Append(db, adminUserId, "", AuditModules.Enrollment, "enroll",
                "enrollment", studentId, $"Enrolled {StudentNames.Display(student)} in {learningClass.Name} ({normalized}).");
            await Task.CompletedTask;
        });
    }

    public async Task SetEnrollmentStatusAsync(string enrollmentId, string status, string adminUserId)
    {
        if (!EnrollmentStatuses.All.Contains(status))
            throw new InvalidOperationException("Invalid enrollment status.");

        await _repository.MutateAsync(async db =>
        {
            var enrollment = db.Enrollments.FirstOrDefault(e => e.EnrollmentId == enrollmentId)
                             ?? throw new InvalidOperationException("Enrollment not found.");
            var previous = enrollment.Status;
            enrollment.Status = status;
            if (status == EnrollmentStatuses.Active)
                enrollment.ApprovedBy = adminUserId;

            if (!string.IsNullOrWhiteSpace(enrollment.ClassId) &&
                EnrollmentStatuses.CountsTowardCapacity(status) &&
                !EnrollmentStatuses.CountsTowardCapacity(previous))
            {
                var learningClass = db.Classes.FirstOrDefault(c => c.ClassId == enrollment.ClassId);
                if (learningClass != null && ClassService.OccupiedSeats(db, learningClass.ClassId) > learningClass.Capacity)
                    throw new InvalidOperationException($"Class \"{learningClass.Name}\" is at capacity.");
            }

            AuditService.Append(db, adminUserId, "", AuditModules.Enrollment, "status",
                "enrollment", enrollmentId, $"Enrollment status {previous} → {status}.");
            await Task.CompletedTask;
        });
    }

    public async Task AssignClassAsync(string enrollmentId, string classId)
    {
        await _repository.MutateAsync(async db =>
        {
            var enrollment = db.Enrollments.FirstOrDefault(e => e.EnrollmentId == enrollmentId)
                             ?? throw new InvalidOperationException("Enrollment not found.");
            var learningClass = db.Classes.FirstOrDefault(c => c.ClassId == classId)
                                ?? throw new InvalidOperationException("Class not found.");

            if (enrollment.ClassId != classId &&
                EnrollmentStatuses.CountsTowardCapacity(enrollment.Status) &&
                ClassService.OccupiedSeats(db, classId) >= learningClass.Capacity)
                throw new InvalidOperationException($"Class \"{learningClass.Name}\" is at capacity.");

            enrollment.ClassId = classId;
            enrollment.ProgramId = learningClass.ProgramId;
            enrollment.TeacherId = learningClass.TeacherId;

            var student = db.Students.FirstOrDefault(s => s.StudentId == enrollment.StudentId);
            if (student != null)
            {
                var programName = ClassService.ProgramName(db, learningClass.ProgramId);
                student.Program = programName;
                student.GradeLevel = programName;
            }

            await Task.CompletedTask;
        });
    }

    private static LearningClass? ResolveClass(HirayaDatabase db, string? classId, string preferredProgram)
    {
        if (!string.IsNullOrWhiteSpace(classId))
            return db.Classes.FirstOrDefault(c => c.ClassId == classId);

        if (string.IsNullOrWhiteSpace(preferredProgram))
            return db.Classes.FirstOrDefault(c =>
                string.Equals(c.Status, "active", StringComparison.OrdinalIgnoreCase));

        return db.Classes.FirstOrDefault(c =>
        {
            var program = ClassService.ProgramName(db, c.ProgramId);
            return string.Equals(c.Name, preferredProgram, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(program, preferredProgram, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string DefaultPeriod()
    {
        var year = DateTime.Today.Month >= 6 ? DateTime.Today.Year : DateTime.Today.Year - 1;
        return $"{year}–{year + 1}";
    }

    private static int ComputeAge(string birthdate)
    {
        if (!DateTime.TryParse(birthdate, out var dob)) return 0;
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;
        return Math.Max(age, 0);
    }
}

public static class IdFactory
{
    public static string New(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}"[..18];
}
