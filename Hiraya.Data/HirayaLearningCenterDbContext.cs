using System.Text.Json;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;
using Microsoft.EntityFrameworkCore;

namespace Hiraya.Data;

public class HirayaLearningCenterDbContext : DbContext
{
    public HirayaLearningCenterDbContext(DbContextOptions<HirayaLearningCenterDbContext> options)
        : base(options)
    {
    }

    public DbSet<HirayaUser> Users => Set<HirayaUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<LearningProgram> Programs => Set<LearningProgram>();
    public DbSet<LearningClass> Classes => Set<LearningClass>();
    public DbSet<CenterRoom> Rooms => Set<CenterRoom>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EnrollmentApplication> EnrollmentApplications => Set<EnrollmentApplication>();
    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();
    public DbSet<ProgressReport> ProgressRecords => Set<ProgressReport>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<TeacherWorkShift> TeacherWorkShifts => Set<TeacherWorkShift>();
    public DbSet<TeacherRoleOption> TeacherRoleOptions => Set<TeacherRoleOption>();
    public DbSet<NewsItem> News => Set<NewsItem>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SystemAlert> Notifications => Set<SystemAlert>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<CenterSettings> Settings => Set<CenterSettings>();
    public DbSet<LearningModule> LearningModules => Set<LearningModule>();
    public DbSet<LearningModuleVersion> LearningModuleVersions => Set<LearningModuleVersion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(191);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Key<HirayaUser>(modelBuilder, "users", x => x.UserId);
        modelBuilder.Entity<HirayaUser>(e =>
        {
            e.Property(x => x.Password).HasColumnName("PasswordHash").HasMaxLength(512);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username);
            e.HasIndex(x => x.Status);
        });

        Key<AppRole>(modelBuilder, "roles", x => x.RoleId);
        modelBuilder.Entity<AppRole>().HasIndex(x => x.Code).IsUnique();

        Key<RolePermission>(modelBuilder, "role_permissions", x => x.PermissionId);
        modelBuilder.Entity<RolePermission>().HasIndex(x => new { x.Role, x.Permission }).IsUnique();

        Key<TeacherProfile>(modelBuilder, "teachers", x => x.ProfileId);
        modelBuilder.Entity<TeacherProfile>().HasIndex(x => x.UserId);
        modelBuilder.Entity<TeacherProfile>().HasIndex(x => x.EmployeeCode);

        Key<Student>(modelBuilder, "students", x => x.StudentId);
        modelBuilder.Entity<Student>(e =>
        {
            e.HasIndex(x => x.ParentId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.QrCode);
        });

        Key<LearningProgram>(modelBuilder, "programs", x => x.ProgramId);
        modelBuilder.Entity<LearningProgram>().HasIndex(x => x.Name);

        Key<LearningClass>(modelBuilder, "classes", x => x.ClassId);
        modelBuilder.Entity<LearningClass>().HasIndex(x => x.ProgramId);
        modelBuilder.Entity<LearningClass>().HasIndex(x => x.TeacherId);

        Key<CenterRoom>(modelBuilder, "rooms", x => x.RoomId);
        modelBuilder.Entity<CenterRoom>().HasIndex(x => x.RoomName);

        Key<Enrollment>(modelBuilder, "enrollments", x => x.EnrollmentId);
        modelBuilder.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => x.ClassId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EnrollmentDate);
        });

        Key<EnrollmentApplication>(modelBuilder, "enrollment_applications", x => x.ApplicationId);

        Key<AttendanceRecord>(modelBuilder, "attendance", x => x.AttendanceId);
        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.ClassId);
            e.HasIndex(x => x.AttendanceDate);
        });

        Key<ProgressReport>(modelBuilder, "progress_records", x => x.ReportId);
        modelBuilder.Entity<ProgressReport>(e =>
        {
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => x.TeacherId);
            e.Property(x => x.History).HasConversion(JsonLists.For<ProgressHistoryEntry>());
            e.Property(x => x.History).Metadata.SetValueComparer(JsonLists.Comparer<ProgressHistoryEntry>());
            e.Property(x => x.Evaluation).HasColumnType("text");
            e.Property(x => x.BehaviorReport).HasColumnType("text");
            e.Property(x => x.DevelopmentalReport).HasColumnType("text");
        });

        Key<Schedule>(modelBuilder, "schedules", x => x.ScheduleId);
        modelBuilder.Entity<Schedule>(e =>
        {
            e.HasIndex(x => x.ClassId);
            e.HasIndex(x => x.TeacherId);
            e.HasIndex(x => x.Room);
            e.HasIndex(x => x.StartAt);
        });

        Key<TeacherWorkShift>(modelBuilder, "teacher_work_shifts", x => x.ShiftId);
        modelBuilder.Entity<TeacherWorkShift>().HasIndex(x => x.TeacherId);

        Key<TeacherRoleOption>(modelBuilder, "teacher_role_options", x => x.RoleId);
        Key<NewsItem>(modelBuilder, "announcements", x => x.NewsId);
        modelBuilder.Entity<NewsItem>().Property(x => x.Body).HasColumnType("text");

        Key<LeaveRequest>(modelBuilder, "leave_requests", x => x.LeaveId);
        Key<Payment>(modelBuilder, "payments", x => x.PaymentId);
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.StudentId);
            e.HasIndex(x => x.EnrollmentId);
            e.HasIndex(x => x.PaymentDate);
            e.HasIndex(x => x.PaymentStatus);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.AmountPaid).HasPrecision(18, 2);
            e.Property(x => x.History).HasConversion(JsonLists.For<PaymentHistoryEntry>());
            e.Property(x => x.History).Metadata.SetValueComparer(JsonLists.Comparer<PaymentHistoryEntry>());
        });

        Key<SystemAlert>(modelBuilder, "notifications", x => x.AlertId);
        modelBuilder.Entity<SystemAlert>().HasIndex(x => x.RecipientId);

        Key<AuditLogEntry>(modelBuilder, "audit_logs", x => x.AuditId);
        modelBuilder.Entity<AuditLogEntry>().HasIndex(x => x.CreatedAt);
        modelBuilder.Entity<AuditLogEntry>().HasIndex(x => x.ActorId);
        modelBuilder.Entity<AuditLogEntry>().Property(x => x.Summary).HasColumnType("text");

        modelBuilder.Entity<CenterSettings>(e =>
        {
            e.ToTable("system_settings");
            e.HasKey(x => x.SettingsId);
            e.Property(x => x.Description).HasColumnType("text");
        });

        Key<LearningModule>(modelBuilder, "learning_modules", x => x.ModuleId);
        modelBuilder.Entity<LearningModule>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ProgramId);
            e.Property(x => x.Description).HasColumnType("text");
            e.Property(x => x.FilePath).HasMaxLength(512);
            e.Property(x => x.ClassIds).HasConversion(JsonLists.For<string>());
            e.Property(x => x.ClassIds).Metadata.SetValueComparer(JsonLists.Comparer<string>());
        });

        Key<LearningModuleVersion>(modelBuilder, "learning_module_versions", x => x.VersionId);
        modelBuilder.Entity<LearningModuleVersion>(e =>
        {
            e.HasIndex(x => x.ModuleId);
            e.Property(x => x.ChangeDescription).HasColumnType("text");
            e.Property(x => x.FilePath).HasMaxLength(512);
        });
    }

    private static void Key<T>(ModelBuilder modelBuilder, string table, System.Linq.Expressions.Expression<Func<T, object?>> key)
        where T : class
    {
        modelBuilder.Entity<T>(e =>
        {
            e.ToTable(table);
            e.HasKey(key);
        });
    }
}

internal static class JsonLists
{
    public static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<List<T>, string> For<T>() =>
        new(
            v => JsonSerializer.Serialize(v ?? new List<T>(), JsonDefaults.Options),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<T>()
                : JsonSerializer.Deserialize<List<T>>(v, JsonDefaults.Options) ?? new List<T>());

    public static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<T>> Comparer<T>() =>
        new(
            (a, b) => JsonSerializer.Serialize(a ?? new List<T>(), JsonDefaults.Options) ==
                      JsonSerializer.Serialize(b ?? new List<T>(), JsonDefaults.Options),
            v => JsonSerializer.Serialize(v ?? new List<T>(), JsonDefaults.Options).GetHashCode(),
            v => JsonSerializer.Deserialize<List<T>>(
                     JsonSerializer.Serialize(v ?? new List<T>(), JsonDefaults.Options),
                     JsonDefaults.Options) ?? new List<T>());
}
