using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class ProgramService
{
    private readonly IHirayaRepository _repository;

    public ProgramService(IHirayaRepository repository) => _repository = repository;

    public async Task SaveAsync(LearningProgram input, HirayaUser actor)
    {
        if (!Navigation.CanManageEnrollment(actor.Role))
            throw new InvalidOperationException("You cannot manage programs.");
        var name = input.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Program name is required.");

        await _repository.MutateAsync(async db =>
        {
            var id = string.IsNullOrWhiteSpace(input.ProgramId) ? IdFactory.New("prg") : input.ProgramId;
            if (db.Programs.Any(p =>
                    p.ProgramId != id &&
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A program with that name already exists.");

            db.Programs.RemoveAll(p => p.ProgramId == id);
            db.Programs.Add(new LearningProgram
            {
                ProgramId = id,
                Name = name,
                Description = input.Description.Trim(),
                Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status.Trim()
            });
            AuditService.Append(db, actor, AuditModules.Programs,
                string.IsNullOrWhiteSpace(input.ProgramId) ? "create" : "update",
                "program", id, $"{actor.Fullname} saved program {name}.");
            await Task.CompletedTask;
        });
    }

    public async Task ArchiveAsync(string programId, HirayaUser actor)
    {
        if (!Navigation.CanManageEnrollment(actor.Role))
            throw new InvalidOperationException("You cannot manage programs.");
        await _repository.MutateAsync(async db =>
        {
            var program = db.Programs.FirstOrDefault(p => p.ProgramId == programId)
                          ?? throw new InvalidOperationException("Program was not found.");
            program.Status = "archived";
            AuditService.Append(db, actor, AuditModules.Programs, "archive", "program", programId,
                $"{actor.Fullname} archived program {program.Name}.");
            await Task.CompletedTask;
        });
    }
}
