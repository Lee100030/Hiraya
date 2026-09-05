using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class ClassSaveRequest
{
    public string ClassId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProgramId { get; set; } = "";
    public string NewProgramName { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string Room { get; set; } = "";
    public int Capacity { get; set; } = 15;
    public string ScheduleNotes { get; set; } = "";
    public string Status { get; set; } = "active";
}

public class ClassService
{
    private readonly IHirayaRepository _repository;

    public ClassService(IHirayaRepository repository) => _repository = repository;

    public static int OccupiedSeats(HirayaDatabase db, string classId) =>
        db.Enrollments.Count(e =>
            e.ClassId == classId && EnrollmentStatuses.CountsTowardCapacity(e.Status));

    public static string ProgramName(HirayaDatabase db, string programId) =>
        db.Programs.FirstOrDefault(p => p.ProgramId == programId)?.Name ?? "";

    public async Task SaveAsync(ClassSaveRequest input, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageEnrollment(actor?.Role))
            throw new InvalidOperationException("You cannot manage classes.");
        var name = input.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Class name is required.");
        if (input.Capacity < 1)
            throw new InvalidOperationException("Capacity must be at least 1.");

        await _repository.MutateAsync(async db =>
        {
            var classId = string.IsNullOrWhiteSpace(input.ClassId) ? IdFactory.New("cls") : input.ClassId;
            var programId = ResolveProgramId(db, input.ProgramId, input.NewProgramName);
            if (string.IsNullOrWhiteSpace(programId))
                throw new InvalidOperationException("Select or create a program.");

            if (!string.IsNullOrWhiteSpace(input.TeacherId) &&
                !db.Users.Any(u => u.UserId == input.TeacherId && u.Role == UserRoles.Teacher))
                throw new InvalidOperationException("Assigned teacher was not found.");

            var occupied = OccupiedSeats(db, classId);
            if (input.Capacity < occupied)
                throw new InvalidOperationException($"Capacity cannot be below current roster ({occupied}).");

            db.Classes.RemoveAll(c => c.ClassId == classId);
            db.Classes.Add(new LearningClass
            {
                ClassId = classId,
                Name = name,
                ProgramId = programId,
                TeacherId = input.TeacherId.Trim(),
                Room = input.Room.Trim(),
                Capacity = input.Capacity,
                ScheduleNotes = input.ScheduleNotes.Trim(),
                Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status.Trim()
            });

            foreach (var enrollment in db.Enrollments.Where(e => e.ClassId == classId))
            {
                enrollment.ProgramId = programId;
                if (!string.IsNullOrWhiteSpace(input.TeacherId))
                    enrollment.TeacherId = input.TeacherId.Trim();
            }

            AuditService.Append(db, actor, AuditModules.Classes,
                string.IsNullOrWhiteSpace(input.ClassId) ? "create" : "update",
                "class", classId, $"Saved class {name}.");
            await Task.CompletedTask;
        });
    }

    public async Task ArchiveAsync(string classId, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageEnrollment(actor?.Role))
            throw new InvalidOperationException("You cannot manage classes.");
        await _repository.MutateAsync(async db =>
        {
            var item = db.Classes.FirstOrDefault(c => c.ClassId == classId)
                       ?? throw new InvalidOperationException("Class not found.");
            item.Status = "archived";
            AuditService.Append(db, actor, AuditModules.Classes, "archive", "class", classId, $"Archived class {item.Name}.");
            await Task.CompletedTask;
        });
    }

    public async Task RestoreAsync(string classId, HirayaUser? actor = null)
    {
        if (!Navigation.CanManageEnrollment(actor?.Role))
            throw new InvalidOperationException("You cannot manage classes.");
        await _repository.MutateAsync(async db =>
        {
            var item = db.Classes.FirstOrDefault(c => c.ClassId == classId)
                       ?? throw new InvalidOperationException("Class not found.");
            item.Status = "active";
            AuditService.Append(db, actor, AuditModules.Classes, "restore", "class", classId, $"Restored class {item.Name}.");
            await Task.CompletedTask;
        });
    }

    private static string ResolveProgramId(HirayaDatabase db, string programId, string newProgramName)
    {
        var created = newProgramName.Trim();
        if (!string.IsNullOrWhiteSpace(created))
        {
            var existing = db.Programs.FirstOrDefault(p =>
                string.Equals(p.Name, created, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing.ProgramId;

            var id = IdFactory.New("prg");
            db.Programs.Add(new LearningProgram
            {
                ProgramId = id,
                Name = created,
                Status = "active"
            });
            return id;
        }

        return programId.Trim();
    }
}
