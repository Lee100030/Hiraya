namespace Hiraya.Shared.Models;

public static class UserRoles
{
    public const string SuperAdmin = "super_admin";
    public const string Admin = "admin";
    public const string Teacher = "teacher";
    public const string Staff = "staff";
    public const string Parent = "parent";
    public const string Student = "student";

    public static readonly string[] All =
        [SuperAdmin, Admin, Teacher, Staff, Parent, Student];

    public static bool IsAdmin(string? role) =>
        role is SuperAdmin or Admin;
}

public static class HirayaCollections
{
    public const string Root = "hiraya";
    public const string Users = "users";
    public const string TeacherProfiles = "teacherProfiles";
    public const string Students = "students";
    public const string Programs = "programs";
    public const string Classes = "classes";
    public const string Enrollments = "enrollments";
    public const string EnrollmentApplications = "enrollmentApplications";
    public const string Attendance = "attendance";
    public const string Reports = "reports";
    public const string Schedules = "schedules";
    public const string TeacherWorkShifts = "teacherWorkShifts";
    public const string TeacherRoleOptions = "teacherRoleOptions";
    public const string News = "news";
    public const string LeaveRequests = "leaveRequests";
    public const string Payments = "payments";
    public const string Alerts = "alerts";
    public const string AuditLogs = "auditLogs";
    public const string RolePermissions = "rolePermissions";
    public const string Settings = "settings";
}

public class HirayaUser
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Fullname { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = UserRoles.Parent;
    public string Status { get; set; } = "active";
    public string? Phone { get; set; }
    public string Address { get; set; } = "";
    public string Position { get; set; } = "";
    public string ProfileImagePath { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string LastLoginAt { get; set; } = "";
    public bool NotifyAttendance { get; set; } = true;
    public bool NotifyPayments { get; set; } = true;
    public bool NotifySchedule { get; set; } = true;
    public bool NotifyProgress { get; set; } = true;
    public string DateFormat { get; set; } = "MMM d, yyyy";
    public string TimeFormat { get; set; } = "HH:mm";
    public string Theme { get; set; } = "system";
    public string? Password { get; set; }

    public void SyncDisplayName()
    {
        var assembled = string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!string.IsNullOrWhiteSpace(assembled))
            Fullname = assembled;
        if (string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Email))
        {
            var at = Email.IndexOf('@');
            Username = at > 0 ? Email[..at] : Email;
        }
    }
}

public class TeacherProfile
{
    public string ProfileId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string EmployeeCode { get; set; } = "";
    public string Position { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Bio { get; set; } = "";
    public string HireDate { get; set; } = "";
    public string Status { get; set; } = "active";
}

public class Student
{
    public string StudentId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Fullname { get; set; } = "";
    public string Birthdate { get; set; } = "";
    public int Age { get; set; }
    public string Gender { get; set; } = "";
    public string Address { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public string ParentId { get; set; } = "";
    public string EmergencyContact { get; set; } = "";
    public string EmergencyPhone { get; set; } = "";
    public string QrCode { get; set; } = "";
    public string GradeLevel { get; set; } = "";
    public string Program { get; set; } = "";
    public string Status { get; set; } = "active";
    public string EnrollmentDate { get; set; } = "";
    public string Notes { get; set; } = "";
}

public static class EnrollmentStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";

    public static readonly string[] All =
        [Pending, Active, Completed, Cancelled, Archived];

    public static bool CountsTowardCapacity(string? status) =>
        status is Pending or Active or "approved";
}

public class LearningProgram
{
    public string ProgramId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "active";
}

public class LearningClass
{
    public string ClassId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProgramId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string Room { get; set; } = "";
    public int Capacity { get; set; } = 15;
    public string ScheduleNotes { get; set; } = "";
    public string Status { get; set; } = "active";
}

public class Enrollment
{
    public string EnrollmentId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string ProgramId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string Period { get; set; } = "";
    public string Status { get; set; } = EnrollmentStatuses.Pending;
    public string EnrollmentDate { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
}

public class EnrollmentApplication
{
    public string ApplicationId { get; set; } = "";
    public string StudentFullname { get; set; } = "";
    public string StudentBirthdate { get; set; } = "";
    public int StudentAge { get; set; }
    public string StudentGender { get; set; } = "";
    public string ParentFullname { get; set; } = "";
    public string ParentEmail { get; set; } = "";
    public string ParentPhone { get; set; } = "";
    public string PreferredProgram { get; set; } = "";
    public string SubmittedAt { get; set; } = "";
    public string Status { get; set; } = "pending_review";
    public string? StudentNumber { get; set; }
}

public class AttendanceRecord
{
    public string AttendanceId { get; set; } = "";
    public string Kind { get; set; } = AttendanceKinds.Student;
    public string StudentId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string QrCode { get; set; } = "";
    public string TimeIn { get; set; } = "";
    public string? TimeOut { get; set; }
    public string AttendanceDate { get; set; } = "";
    public string Status { get; set; } = AttendanceStatuses.Present;
    public string Notes { get; set; } = "";
    public string RecordedBy { get; set; } = "";
}

public static class AttendanceKinds
{
    public const string Student = "student";
    public const string Employee = "employee";
}

public static class AttendanceStatuses
{
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Late = "late";
    public const string Excused = "excused";
    public const string Leave = "leave";

    public static readonly string[] ForStudents = [Present, Absent, Late, Excused];
    public static readonly string[] ForEmployees = [Present, Absent, Late, Leave];

    public static bool IsPresentLike(string? status) =>
        status is Present or Late;
}

public static class ProgressAreas
{
    public const string Literacy = "literacy";
    public const string Numeracy = "numeracy";
    public const string Behavior = "behavior";
    public const string Social = "social";
    public const string Motor = "motor";
    public const string Overall = "overall";

    public static readonly string[] All = [Literacy, Numeracy, Behavior, Social, Motor, Overall];

    public static string Label(string? area) => area switch
    {
        Literacy => "Literacy",
        Numeracy => "Numeracy",
        Behavior => "Behavior",
        Social => "Social / emotional",
        Motor => "Motor skills",
        Overall => "Overall",
        _ => string.IsNullOrWhiteSpace(area) ? "Overall" : area
    };
}

public static class ProgressRatings
{
    public const string NeedsSupport = "needs_support";
    public const string Emerging = "emerging";
    public const string Developing = "developing";
    public const string Proficient = "proficient";

    public static readonly string[] All = [NeedsSupport, Emerging, Developing, Proficient];

    public static string Label(string? rating) => rating switch
    {
        NeedsSupport => "Needs support",
        Emerging => "Emerging",
        Developing => "Developing",
        Proficient => "Proficient",
        _ => string.IsNullOrWhiteSpace(rating) ? "—" : rating
    };
}

public static class ProgressStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Published, Archived];
}

public class ProgressHistoryEntry
{
    public string EntryId { get; set; } = "";
    public string ChangedBy { get; set; } = "";
    public string ChangedAt { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class ProgressReport
{
    public string ReportId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string ClassId { get; set; } = "";
    public string Period { get; set; } = "";
    public string Area { get; set; } = ProgressAreas.Overall;
    public string Rating { get; set; } = ProgressRatings.Developing;
    public string Evaluation { get; set; } = "";
    public string BehaviorReport { get; set; } = "";
    public string DevelopmentalReport { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string UploadedMedia { get; set; } = "";
    public string Status { get; set; } = ProgressStatuses.Draft;
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public List<ProgressHistoryEntry> History { get; set; } = new();
}

public class Schedule
{
    public string ScheduleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = ScheduleKinds.ClassSession;
    public string ClassId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string ScheduleType { get; set; } = ScheduleKinds.ClassSession;
    public string StartAt { get; set; } = "";
    public string EndAt { get; set; } = "";
    public string Room { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "confirmed";
}

public static class ScheduleKinds
{
    public const string ClassSession = "classSession";
    public const string Activity = "activity";
    public const string Event = "event";
    public const string Assessment = "assessment";
    public const string Holiday = "holiday";
    public const string Deadline = "deadline";
    public const string TeacherTask = "teacherTask";

    public static readonly string[] All =
        [ClassSession, Activity, Event, Assessment, Holiday, Deadline, TeacherTask];

    public static string Label(string? kind) => kind switch
    {
        ClassSession => "Class",
        Activity => "Activity",
        Event => "Event",
        Assessment => "Assessment",
        Holiday => "Holiday",
        Deadline => "Deadline",
        TeacherTask => "My task",
        _ => string.IsNullOrWhiteSpace(kind) ? "Class" : kind
    };
}

public class TeacherWorkShift
{
    public string ShiftId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string Title { get; set; } = "Work shift";
    public string WorkDate { get; set; } = "";
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "17:00";
    public string Room { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "scheduled";
}

public static class PaymentMethods
{
    public const string Cash = "cash";
    public const string Gcash = "gcash";
    public const string BankTransfer = "bankTransfer";
    public const string Check = "check";

    public static readonly string[] All = [Cash, Gcash, BankTransfer, Check];

    public static string Label(string? method) => method switch
    {
        Cash => "Cash",
        Gcash => "GCash",
        BankTransfer => "Bank transfer",
        Check => "Check",
        _ => string.IsNullOrWhiteSpace(method) ? "Cash" : method
    };
}

public static class PaymentStatuses
{
    public const string Pending = "pending";
    public const string Partial = "partial";
    public const string Overdue = "overdue";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Refunded = "refunded";
    public const string Cancelled = "cancelled";

    public static readonly string[] All =
        [Pending, Partial, Overdue, Paid, Failed, Refunded, Cancelled];

    public static readonly string[] Open = [Pending, Partial, Overdue];

    public static bool IsOpen(string? status) =>
        status is Pending or Partial or Overdue;

    public static bool IsCollected(string? status) =>
        status is Paid or Partial;
}

public class PaymentHistoryEntry
{
    public string EntryId { get; set; } = "";
    public string ChangedBy { get; set; } = "";
    public string ChangedAt { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class Payment
{
    public string PaymentId { get; set; } = "";
    public string EnrollmentId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string PaymentStatus { get; set; } = PaymentStatuses.Pending;
    public string ReferenceNumber { get; set; } = "";
    public string PaymentDate { get; set; } = "";
    public string DueAt { get; set; } = "";
    public string PaidAt { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<PaymentHistoryEntry> History { get; set; } = new();
}

public static class NotificationKinds
{
    public const string System = "system";
    public const string Enrollment = "enrollment";
    public const string Attendance = "attendance";
    public const string Progress = "progress";
    public const string Payment = "payment";
    public const string Schedule = "schedule";
    public const string Announcement = "announcement";
    public const string LearningModule = "learning_module";

    public static readonly string[] All =
        [System, Enrollment, Attendance, Progress, Payment, Schedule, Announcement, LearningModule];

    public static string Label(string? kind) => kind switch
    {
        Enrollment => "Enrollment",
        Attendance => "Attendance",
        Progress => "Progress",
        Payment => "Payment",
        Schedule => "Schedule",
        Announcement => "Announcement",
        LearningModule => "Learning module",
        _ => "General"
    };
}

public static class NotificationAudiences
{
    public const string User = "user";
    public const string Admins = "admins";
    public const string Teachers = "teachers";
    public const string Staff = "staff";
    public const string Parents = "parents";
    public const string Employees = "employees";
    public const string Everyone = "everyone";

    public static readonly string[] All =
        [User, Admins, Teachers, Staff, Parents, Employees, Everyone];

    public static string Label(string? audience) => audience switch
    {
        User => "One person",
        Admins => "Administrators",
        Teachers => "Teachers",
        Staff => "Staff",
        Parents => "Parents",
        Employees => "Teachers and staff",
        Everyone => "Everyone",
        _ => "Everyone"
    };
}

public class SystemAlert
{
    public string AlertId { get; set; } = "";
    public string Kind { get; set; } = NotificationKinds.System;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public string Href { get; set; } = "";
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = "";
    public bool Read { get; set; }
}

public class NewsItem
{
    public string NewsId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public bool IsPublished { get; set; } = true;
    public string Status { get; set; } = "published";
    public string CreatedBy { get; set; } = "";
}

public class TeacherRoleOption
{
    public string RoleId { get; set; } = "";
    public string Name { get; set; } = "";
}

public class LeaveRequest
{
    public string LeaveId { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string Email { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string SubmittedAt { get; set; } = "";
    public string? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? AdminNote { get; set; }
}

public class HirayaDatabase
{
    public List<HirayaUser> Users { get; set; } = new();
    public List<TeacherProfile> TeacherProfiles { get; set; } = new();
    public List<Student> Students { get; set; } = new();
    public List<LearningProgram> Programs { get; set; } = new();
    public List<LearningClass> Classes { get; set; } = new();
    public List<Enrollment> Enrollments { get; set; } = new();
    public List<EnrollmentApplication> EnrollmentApplications { get; set; } = new();
    public List<AttendanceRecord> Attendance { get; set; } = new();
    public List<ProgressReport> Reports { get; set; } = new();
    public List<Schedule> Schedules { get; set; } = new();
    public List<TeacherWorkShift> TeacherWorkShifts { get; set; } = new();
    public List<TeacherRoleOption> TeacherRoleOptions { get; set; } = new();
    public List<NewsItem> News { get; set; } = new();
    public List<LeaveRequest> LeaveRequests { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<SystemAlert> Alerts { get; set; } = new();
    public List<AuditLogEntry> AuditLogs { get; set; } = new();
    public List<RolePermission> RolePermissions { get; set; } = new();
    public List<AppRole> Roles { get; set; } = new();
    public List<CenterRoom> Rooms { get; set; } = new();
    public List<LearningModule> LearningModules { get; set; } = new();
    public List<LearningModuleVersion> LearningModuleVersions { get; set; } = new();
    public CenterSettings Settings { get; set; } = new();
}

public class AppRole
{
    public string RoleId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class CenterRoom
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public int Capacity { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "active";
}

public class RolePermission
{
    public string PermissionId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Permission { get; set; } = "";
    public bool Allowed { get; set; }
}

public class CenterSettings
{
    public string SettingsId { get; set; } = "center";
    public string CenterName { get; set; } = "HIRAYA Learning Center";
    public string LogoPath { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string Description { get; set; } = "";
    public string TimeZone { get; set; } = "Asia/Manila";
    public string DefaultPeriod { get; set; } = "";
    public string BusinessHours { get; set; } = "Monday–Friday, 8:00–17:00";
    public string DefaultAttendanceStatus { get; set; } = AttendanceStatuses.Present;
    public string DefaultPaymentMethod { get; set; } = PaymentMethods.Cash;
    public bool NotificationsEnabled { get; set; } = true;
    public bool NotifyAttendance { get; set; } = true;
    public bool NotifyPayments { get; set; } = true;
    public bool NotifySchedule { get; set; } = true;
    public bool NotifyProgress { get; set; } = true;
    public string ModuleAllowedExtensions { get; set; } = "";
    public int ModuleMaxFileMb { get; set; }
    public string UpdatedAt { get; set; } = "";
}

public static class AuditModules
{
    public const string Auth = "auth";
    public const string Students = "students";
    public const string Staff = "staff";
    public const string Classes = "classes";
    public const string Enrollment = "enrollment";
    public const string Attendance = "attendance";
    public const string Schedule = "schedule";
    public const string Progress = "progress";
    public const string Payments = "payments";
    public const string Notifications = "notifications";
    public const string Announcements = "announcements";
    public const string Users = "users";
    public const string Roles = "roles";
    public const string Settings = "settings";
    public const string Programs = "programs";
    public const string LearningModules = "learning_modules";

    public static readonly string[] All =
        [Auth, Students, Staff, Classes, Enrollment, Attendance, Schedule, Progress, Payments, Notifications, Announcements, Users, Roles, Settings, Programs, LearningModules];

    public static string Label(string? module) => module switch
    {
        Auth => "Sign-in",
        Students => "Students",
        Staff => "Staff",
        Classes => "Classes",
        Enrollment => "Enrollment",
        Attendance => "Attendance",
        Schedule => "Schedule",
        Progress => "Progress",
        Payments => "Payments",
        Notifications => "Notifications",
        Announcements => "Announcements",
        Users => "Users",
        Roles => "Roles",
        Settings => "Settings",
        Programs => "Programs",
        LearningModules => "Learning modules",
        _ => string.IsNullOrWhiteSpace(module) ? "System" : module
    };
}

public class AuditLogEntry
{
    public string AuditId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Summary { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public static class TeacherRoleCatalog
{
    public static readonly string[] Defaults =
    [
        "Classroom Teacher",
        "Reading Specialist",
        "Learning Support",
        "Early Childhood",
        "Special Education"
    ];

    public static List<string> Resolve(HirayaDatabase db)
    {
        var fromDb = db.TeacherRoleOptions
            .Select(r => r.Name.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n));
        var fromProfiles = db.TeacherProfiles
            .Select(p => p.Specialty.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n));
        return Defaults
            .Concat(fromDb)
            .Concat(fromProfiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }
}

public static class Navigation
{
    public static string RoleLabel(string role) => role switch
    {
        UserRoles.SuperAdmin => "Super Administrator",
        UserRoles.Admin => "Administrator",
        UserRoles.Teacher => "Teacher",
        UserRoles.Staff => "Staff",
        UserRoles.Parent => "Parent / Guardian",
        UserRoles.Student => "Student",
        _ => role
    };

    public static bool IsFamilyRole(string? role) =>
        role is UserRoles.Parent or UserRoles.Student;

    public static bool CanAccessAdminPanel(string? role) => UserRoles.IsAdmin(role);

    public static bool CanManageUsers(string? role) => UserRoles.IsAdmin(role);

    public static bool CanManageRoles(string? role) => role == UserRoles.SuperAdmin;

    public static bool CanManageSystemSettings(string? role) => UserRoles.IsAdmin(role);

    public static bool CanAdministerAccount(string? actorRole, string? targetRole)
    {
        if (!CanManageUsers(actorRole))
            return false;
        if (targetRole == UserRoles.SuperAdmin && actorRole != UserRoles.SuperAdmin)
            return false;
        return true;
    }

    public static bool CanAssignRole(string? actorRole, string? newRole)
    {
        if (newRole == UserRoles.SuperAdmin)
            return actorRole == UserRoles.SuperAdmin;
        return CanManageUsers(actorRole);
    }

    public static bool CanManageStudents(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanManageStaff(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanViewStaffDirectory(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff;

    public static bool CanViewOperations(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff;

    public static bool CanManageEnrollment(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanViewClasses(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff;

    public static bool CanViewEnrollment(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff or UserRoles.Parent;

    public static bool CanViewAttendance(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff or UserRoles.Parent or UserRoles.Student;

    public static bool CanTakeAttendance(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff;

    public static bool CanViewSchedule(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff or UserRoles.Parent or UserRoles.Student;

    public static bool CanManageSchedule(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanViewProgress(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff or UserRoles.Parent or UserRoles.Student;

    public static bool CanWriteProgress(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher;

    public static bool CanViewPayments(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff or UserRoles.Parent or UserRoles.Student;

    public static bool CanManagePayments(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff;

    public static bool CanViewNotifications(string? role) =>
        !string.IsNullOrWhiteSpace(role);

    public static bool CanSendNotifications(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff;

    public static bool CanManageAnnouncements(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanViewReports(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff or UserRoles.Parent;

    public static bool CanViewAudit(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanManageLearningModules(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin;

    public static bool CanViewLearningModules(string? role) =>
        role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Parent or UserRoles.Student;

    public static bool IsEmployeeRole(string? role) =>
        role is UserRoles.Teacher or UserRoles.Staff;
}

public static class StudentNames
{
    public static string Display(Student student)
    {
        if (!string.IsNullOrWhiteSpace(student.Fullname))
            return student.Fullname;

        return string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static void SyncFullName(Student student)
    {
        if (string.IsNullOrWhiteSpace(student.FirstName) && string.IsNullOrWhiteSpace(student.LastName))
            return;
        student.Fullname = Display(new Student
        {
            FirstName = student.FirstName,
            MiddleName = student.MiddleName,
            LastName = student.LastName,
            Fullname = ""
        });
    }
}
