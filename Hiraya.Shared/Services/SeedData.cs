using Hiraya.Shared.Models;
using Hiraya.Shared.Security;

namespace Hiraya.Shared.Services;

public static class SeedData
{
    public const string SuperAdminId = "usr_super_admin";

    public static HirayaDatabase Create()
    {
        return new HirayaDatabase
        {
            Users =
            [
                new HirayaUser
                {
                    UserId = SuperAdminId,
                    FirstName = "Maria",
                    LastName = "Santos",
                    Fullname = "Maria Santos",
                    Username = "admin",
                    Email = "admin@hiraya.local",
                    Role = UserRoles.SuperAdmin,
                    Status = "active",
                    Phone = "09170000001",
                    Position = "Center Director",
                    CreatedAt = "2024-01-01T00:00:00.000Z",
                    Password = PasswordHasher.Hash("Admin123")
                },
                new HirayaUser
                {
                    UserId = "usr_admin_001",
                    FirstName = "Lea",
                    LastName = "Cruz",
                    Fullname = "Lea Cruz",
                    Username = "ops",
                    Email = "ops@hiraya.local",
                    Role = UserRoles.Admin,
                    Status = "active",
                    Phone = "09170000005",
                    Position = "Operations Administrator",
                    CreatedAt = "2024-01-01T00:00:00.000Z",
                    Password = PasswordHasher.Hash("Admin123")
                },
                new HirayaUser
                {
                    UserId = "usr_teacher_001",
                    FirstName = "Ana",
                    LastName = "Reyes",
                    Fullname = "Ana Reyes",
                    Username = "teacher",
                    Email = "teacher@hiraya.local",
                    Role = UserRoles.Teacher,
                    Status = "active",
                    Phone = "09170000002",
                    Position = "Lead Teacher",
                    CreatedAt = "2024-01-01T00:00:00.000Z",
                    Password = PasswordHasher.Hash("Teacher123")
                },
                new HirayaUser
                {
                    UserId = "usr_staff_001",
                    FirstName = "Rico",
                    LastName = "Mendoza",
                    Fullname = "Rico Mendoza",
                    Username = "staff",
                    Email = "staff@hiraya.local",
                    Role = UserRoles.Staff,
                    Status = "active",
                    Phone = "09170000006",
                    Position = "Front Desk",
                    CreatedAt = "2024-01-01T00:00:00.000Z",
                    Password = PasswordHasher.Hash("Staff123")
                },
                new HirayaUser
                {
                    UserId = "usr_parent_001",
                    FirstName = "Juan",
                    LastName = "Dela Cruz",
                    Fullname = "Juan Dela Cruz",
                    Username = "parent",
                    Email = "parent@hiraya.local",
                    Role = UserRoles.Parent,
                    Status = "active",
                    Phone = "09170000004",
                    CreatedAt = "2024-01-01T00:00:00.000Z",
                    Password = PasswordHasher.Hash("Parent123")
                }
            ],
            TeacherProfiles =
            [
                new TeacherProfile
                {
                    ProfileId = "tprof_001",
                    UserId = "usr_teacher_001",
                    EmployeeCode = "T-001",
                    Position = "Lead Teacher",
                    Specialty = "Early Childhood",
                    Phone = "09170000002",
                    Bio = "Lead classroom teacher.",
                    HireDate = "2022-03-01T00:00:00.000Z",
                    Status = "active"
                },
                new TeacherProfile
                {
                    ProfileId = "tprof_002",
                    UserId = "usr_staff_001",
                    EmployeeCode = "S-001",
                    Position = "Front Desk",
                    Specialty = "",
                    Phone = "09170000006",
                    Bio = "Operations support.",
                    HireDate = "2024-01-15T00:00:00.000Z",
                    Status = "active"
                }
            ],
            Students =
            [
                new Student
                {
                    StudentId = "stu_001",
                    FirstName = "Sofia",
                    LastName = "Dela Cruz",
                    Fullname = "Sofia Dela Cruz",
                    Birthdate = "2020-03-15T00:00:00.000Z",
                    Age = 6,
                    Gender = "female",
                    Address = "Quezon City",
                    ContactNumber = "09170000004",
                    ParentId = "usr_parent_001",
                    EmergencyContact = "Juan Dela Cruz",
                    EmergencyPhone = "09170000004",
                    QrCode = "HIRAYA-STU-001",
                    GradeLevel = "Kinder",
                    Program = "Kinder",
                    Status = "active",
                    EnrollmentDate = "2025-06-01T08:00:00.000Z",
                    Notes = "Reading support twice weekly"
                },
                new Student
                {
                    StudentId = "stu_002",
                    FirstName = "Miguel",
                    LastName = "Torres",
                    Fullname = "Miguel Torres",
                    Birthdate = "2019-08-22T00:00:00.000Z",
                    Age = 7,
                    Gender = "male",
                    Address = "Quezon City",
                    ParentId = "usr_parent_001",
                    QrCode = "HIRAYA-STU-002",
                    GradeLevel = "Grade 1",
                    Program = "Grade 1",
                    Status = "pending",
                    Notes = "Pending enrollment approval"
                }
            ],
            Programs =
            [
                new LearningProgram { ProgramId = "prg_kinder", Name = "Kinder", Description = "Kindergarten program", Status = "active" },
                new LearningProgram { ProgramId = "prg_g1", Name = "Grade 1", Description = "Grade 1 program", Status = "active" }
            ],
            Classes =
            [
                new LearningClass
                {
                    ClassId = "cls_kinder_a",
                    Name = "Kinder A",
                    ProgramId = "prg_kinder",
                    TeacherId = "usr_teacher_001",
                    Room = "Room 1",
                    Capacity = 12,
                    ScheduleNotes = "Mon–Fri 8:00–12:00",
                    Status = "active"
                },
                new LearningClass
                {
                    ClassId = "cls_g1_a",
                    Name = "Grade 1 A",
                    ProgramId = "prg_g1",
                    TeacherId = "usr_teacher_001",
                    Room = "Room 2",
                    Capacity = 15,
                    ScheduleNotes = "Mon–Fri 8:00–15:00",
                    Status = "active"
                }
            ],
            Enrollments =
            [
                new Enrollment
                {
                    EnrollmentId = "enr_001",
                    StudentId = "stu_001",
                    ClassId = "cls_kinder_a",
                    ProgramId = "prg_kinder",
                    TeacherId = "usr_teacher_001",
                    Period = "2025–2026",
                    Status = EnrollmentStatuses.Active,
                    EnrollmentDate = "2025-06-01T08:00:00.000Z",
                    ApprovedBy = SuperAdminId
                },
                new Enrollment
                {
                    EnrollmentId = "enr_002",
                    StudentId = "stu_002",
                    ClassId = "cls_g1_a",
                    ProgramId = "prg_g1",
                    TeacherId = "usr_teacher_001",
                    Period = "2026–2027",
                    Status = EnrollmentStatuses.Pending,
                    EnrollmentDate = "2026-07-10T09:30:00.000Z",
                    ApprovedBy = ""
                }
            ],
            EnrollmentApplications =
            [
                new EnrollmentApplication
                {
                    ApplicationId = "app_demo_001",
                    StudentFullname = "Luna Reyes",
                    StudentBirthdate = "2021-01-08",
                    StudentAge = 5,
                    StudentGender = "female",
                    ParentFullname = "Carla Reyes",
                    ParentEmail = "carla.reyes@example.com",
                    ParentPhone = "09170000009",
                    PreferredProgram = "Kinder",
                    SubmittedAt = DateTime.UtcNow.AddDays(-1).ToString("o"),
                    Status = "pending_review"
                }
            ],
            Attendance =
            [
                new AttendanceRecord
                {
                    AttendanceId = "att_001",
                    Kind = AttendanceKinds.Student,
                    StudentId = "stu_001",
                    ClassId = "cls_kinder_a",
                    QrCode = "HIRAYA-STU-001",
                    TimeIn = DateTime.UtcNow.Date.AddHours(7).AddMinutes(45).ToString("o"),
                    TimeOut = null,
                    AttendanceDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                    Status = AttendanceStatuses.Present,
                    Notes = ""
                }
            ],
            Reports =
            [
                new ProgressReport
                {
                    ReportId = "rep_001",
                    StudentId = "stu_001",
                    TeacherId = "usr_teacher_001",
                    ClassId = "cls_kinder_a",
                    Period = "2025–2026 Q1",
                    Area = ProgressAreas.Literacy,
                    Rating = ProgressRatings.Developing,
                    Evaluation = "Making steady progress in reading fluency.",
                    BehaviorReport = "Cooperative and engaged.",
                    DevelopmentalReport = "Age-appropriate motor skills.",
                    Remarks = "Continue weekly sessions.",
                    UploadedMedia = "",
                    Status = ProgressStatuses.Published,
                    CreatedAt = DateTime.UtcNow.AddDays(-21).ToString("o"),
                    UpdatedAt = DateTime.UtcNow.AddDays(-3).ToString("o"),
                    History =
                    [
                        new ProgressHistoryEntry
                        {
                            EntryId = "ph_001",
                            ChangedBy = "usr_teacher_001",
                            ChangedAt = DateTime.UtcNow.AddDays(-21).ToString("o"),
                            Action = "created",
                            Summary = "Record created."
                        },
                        new ProgressHistoryEntry
                        {
                            EntryId = "ph_002",
                            ChangedBy = "usr_teacher_001",
                            ChangedAt = DateTime.UtcNow.AddDays(-3).ToString("o"),
                            Action = "published",
                            Summary = "rating → Developing; evaluation updated; status → published"
                        }
                    ]
                },
                new ProgressReport
                {
                    ReportId = "rep_002",
                    StudentId = "stu_001",
                    TeacherId = "usr_teacher_001",
                    ClassId = "cls_kinder_a",
                    Period = "2025–2026 Q1",
                    Area = ProgressAreas.Numeracy,
                    Rating = ProgressRatings.Emerging,
                    Evaluation = "Recognizes numbers 1–10; still working on counting on.",
                    BehaviorReport = "",
                    DevelopmentalReport = "",
                    Remarks = "Use manipulatives during morning work.",
                    Status = ProgressStatuses.Draft,
                    CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("o"),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2).ToString("o"),
                    History =
                    [
                        new ProgressHistoryEntry
                        {
                            EntryId = "ph_003",
                            ChangedBy = "usr_teacher_001",
                            ChangedAt = DateTime.UtcNow.AddDays(-2).ToString("o"),
                            Action = "created",
                            Summary = "Record created."
                        }
                    ]
                }
            ],
            Schedules =
            [
                new Schedule
                {
                    ScheduleId = "sch_001",
                    Title = "Kinder A · Morning class",
                    Kind = ScheduleKinds.ClassSession,
                    ScheduleType = ScheduleKinds.ClassSession,
                    ClassId = "cls_kinder_a",
                    StudentId = "",
                    TeacherId = "usr_teacher_001",
                    StartAt = DateTime.SpecifyKind(DateTime.Today.AddDays(1).AddHours(9), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    EndAt = DateTime.SpecifyKind(DateTime.Today.AddDays(1).AddHours(11), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    Room = "Room 1",
                    Notes = "",
                    Status = "confirmed"
                },
                new Schedule
                {
                    ScheduleId = "sch_holiday_001",
                    Title = "National holiday",
                    Kind = ScheduleKinds.Holiday,
                    ScheduleType = ScheduleKinds.Holiday,
                    StartAt = DateTime.SpecifyKind(DateTime.Today.AddDays(10), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    EndAt = DateTime.SpecifyKind(DateTime.Today.AddDays(10).AddHours(23), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    Status = "confirmed"
                }
            ],
            TeacherWorkShifts = CreateSampleWorkShifts(),
            TeacherRoleOptions = TeacherRoleCatalog.Defaults
                .Select((name, i) => new TeacherRoleOption
                {
                    RoleId = $"trole_{i + 1:000}",
                    Name = name
                }).ToList(),
            News =
            [
                new NewsItem
                {
                    NewsId = "news_001",
                    Title = "Welcome to HIRAYA Learning Center",
                    Body = "HLCMS is ready to support daily operations, learning programs, and family partnership.",
                    PublishedAt = DateTime.UtcNow.AddDays(-2).ToString("o"),
                    IsPublished = true,
                    Status = "published",
                    CreatedBy = SuperAdminId
                },
                new NewsItem
                {
                    NewsId = "news_002",
                    Title = "Picture day reminder",
                    Body = "Picture day is next Friday. Please send a spare uniform.",
                    PublishedAt = "",
                    IsPublished = false,
                    Status = "draft",
                    CreatedBy = SuperAdminId
                }
            ],
            Payments =
            [
                new Payment
                {
                    PaymentId = "pay_001",
                    EnrollmentId = "enr_001",
                    StudentId = "stu_001",
                    Amount = 3500,
                    AmountPaid = 3500,
                    PaymentMethod = PaymentMethods.Gcash,
                    PaymentStatus = PaymentStatuses.Paid,
                    ReferenceNumber = "GC-1001",
                    DueAt = "2025-06-01T00:00:00.000Z",
                    PaymentDate = "2025-06-02T10:00:00.000Z",
                    PaidAt = "2025-06-02T10:00:00.000Z",
                    RecordedBy = SuperAdminId,
                    Notes = "June tuition"
                },
                new Payment
                {
                    PaymentId = "pay_002",
                    EnrollmentId = "enr_001",
                    StudentId = "stu_001",
                    Amount = 3500,
                    AmountPaid = 0,
                    PaymentMethod = PaymentMethods.Cash,
                    PaymentStatus = PaymentStatuses.Pending,
                    ReferenceNumber = "",
                    DueAt = DateTime.SpecifyKind(DateTime.Today.AddDays(4), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    PaymentDate = DateTime.SpecifyKind(DateTime.Today.AddDays(4), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    RecordedBy = SuperAdminId,
                    Notes = "Next tuition installment"
                },
                new Payment
                {
                    PaymentId = "pay_003",
                    EnrollmentId = "enr_001",
                    StudentId = "stu_001",
                    Amount = 1500,
                    AmountPaid = 500,
                    PaymentMethod = PaymentMethods.Cash,
                    PaymentStatus = PaymentStatuses.Partial,
                    ReferenceNumber = "CASH-88",
                    DueAt = DateTime.SpecifyKind(DateTime.Today.AddDays(-2), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    PaymentDate = DateTime.SpecifyKind(DateTime.Today.AddDays(-2), DateTimeKind.Local).ToUniversalTime().ToString("o"),
                    RecordedBy = SuperAdminId,
                    Notes = "Materials fee"
                }
            ],
            Alerts =
            [
                new SystemAlert
                {
                    AlertId = "al_001",
                    Kind = NotificationKinds.System,
                    Title = "Welcome",
                    Message = "Welcome to HIRAYA Learning Center Management System.",
                    RecipientId = SuperAdminId,
                    Href = "",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    Read = false
                },
                new SystemAlert
                {
                    AlertId = "al_002",
                    Kind = NotificationKinds.Announcement,
                    Title = "Welcome to HIRAYA Learning Center",
                    Message = "HLCMS is ready to support daily operations, learning programs, and family partnership.",
                    RecipientId = "usr_parent_001",
                    Href = "announcements",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("o"),
                    Read = false
                },
                new SystemAlert
                {
                    AlertId = "al_003",
                    Kind = NotificationKinds.Payment,
                    Title = "Open balance",
                    Message = "A materials fee for Sofia Dela Cruz is overdue. Remaining ₱1,000.00.",
                    RecipientId = "usr_parent_001",
                    Href = "payments",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow.AddHours(-6).ToString("o"),
                    Read = false
                }
            ],
            AuditLogs =
            [
                new AuditLogEntry
                {
                    AuditId = "aud_001",
                    ActorId = SuperAdminId,
                    ActorName = "Maria Santos",
                    Module = AuditModules.Auth,
                    Action = "login",
                    EntityType = "user",
                    EntityId = SuperAdminId,
                    Summary = "Maria Santos signed in.",
                    CreatedAt = DateTime.UtcNow.AddHours(-2).ToString("o")
                },
                new AuditLogEntry
                {
                    AuditId = "aud_002",
                    ActorId = SuperAdminId,
                    ActorName = "Maria Santos",
                    Module = AuditModules.Payments,
                    Action = "charge",
                    EntityType = "payment",
                    EntityId = "pay_003",
                    Summary = "Created ₱1,500.00 charge for Sofia Dela Cruz.",
                    CreatedAt = DateTime.UtcNow.AddDays(-2).ToString("o")
                },
                new AuditLogEntry
                {
                    AuditId = "aud_003",
                    ActorId = "usr_teacher_001",
                    ActorName = "Ana Reyes",
                    Module = AuditModules.Progress,
                    Action = "publish",
                    EntityType = "progress",
                    EntityId = "rep_001",
                    Summary = "Progress record published.",
                    CreatedAt = DateTime.UtcNow.AddDays(-3).ToString("o")
                }
            ],
            Settings = new CenterSettings
            {
                CenterName = "HIRAYA Learning Center",
                Address = "Local development center",
                Phone = "09170000000",
                Email = "hello@hiraya.local",
                TimeZone = "Asia/Manila",
                BusinessHours = "Monday–Friday, 8:00–17:00",
                Description = "Child care, therapy, and family connection — growing together with care."
            }
        };
    }

    private static List<TeacherWorkShift> CreateSampleWorkShifts()
    {
        var monday = StartOfWeek(DateTime.Today);
        var shifts = new List<TeacherWorkShift>();
        for (var i = 0; i < 5; i++)
        {
            var day = monday.AddDays(i);
            shifts.Add(new TeacherWorkShift
            {
                ShiftId = $"tws_{i + 1:000}",
                TeacherId = "usr_teacher_001",
                Title = i is 1 or 3 ? "Learning support" : "Classroom duty",
                WorkDate = day.ToString("yyyy-MM-dd"),
                StartTime = "08:00",
                EndTime = i == 4 ? "12:00" : "17:00",
                Room = "Room 1",
                Notes = "",
                Status = "scheduled"
            });
        }

        return shifts;
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
