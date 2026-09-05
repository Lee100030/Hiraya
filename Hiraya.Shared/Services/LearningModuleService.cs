using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class LearningModuleFile
{
    public byte[] Bytes { get; init; } = [];
    public string FileName { get; init; } = "";
    public string ContentType { get; init; } = "application/octet-stream";
}

public class LearningModuleService
{
    private readonly IHirayaRepository _repository;
    private readonly LearningModuleStorage _storage;

    public LearningModuleService(IHirayaRepository repository, LearningModuleStorage storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public static IEnumerable<LearningModule> Visible(HirayaDatabase db, HirayaUser user, bool includeHidden = false)
    {
        var modules = db.LearningModules.AsEnumerable();
        if (Navigation.CanManageLearningModules(user.Role))
        {
            if (!includeHidden)
                modules = modules.Where(m => m.Status != LearningModuleStatuses.Archived);
            return modules.OrderByDescending(m => m.UpdatedAt);
        }

        if (!Navigation.CanViewLearningModules(user.Role))
            return [];

        var classIds = ViewerClassIds(db, user);
        var programIds = db.Classes
            .Where(c => classIds.Contains(c.ClassId))
            .Select(c => c.ProgramId)
            .ToHashSet();

        return modules
            .Where(m => m.Status == LearningModuleStatuses.Published)
            .Where(m => IsAssignedToTeacher(m, classIds, programIds))
            .OrderByDescending(m => m.UpdatedAt);
    }

    public static LearningModule? Find(HirayaDatabase db, string moduleId) =>
        db.LearningModules.FirstOrDefault(m => m.ModuleId == moduleId);

    public static bool CanAccess(HirayaDatabase db, HirayaUser user, LearningModule module)
    {
        if (Navigation.CanManageLearningModules(user.Role))
            return true;
        if (!Navigation.CanViewLearningModules(user.Role))
            return false;
        if (module.Status != LearningModuleStatuses.Published)
            return false;
        var classIds = ViewerClassIds(db, user);
        var programIds = db.Classes.Where(c => classIds.Contains(c.ClassId)).Select(c => c.ProgramId).ToHashSet();
        return IsAssignedToTeacher(module, classIds, programIds);
    }

    public static HashSet<string> AssignedClassIds(HirayaDatabase db, string teacherId) =>
        db.Classes
            .Where(c => c.TeacherId == teacherId && !string.Equals(c.Status, "archived", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.ClassId)
            .ToHashSet();

    public static HashSet<string> ViewerClassIds(HirayaDatabase db, HirayaUser user)
    {
        if (user.Role == UserRoles.Teacher)
            return AssignedClassIds(db, user.UserId);
        if (Navigation.IsFamilyRole(user.Role))
            return FamilyPortal.ClassIdsForStudents(db, FamilyPortal.StudentIds(db, user));
        return [];
    }

    public async Task<LearningModule> SaveAsync(
        LearningModuleSaveRequest input,
        HirayaUser actor,
        byte[]? fileBytes,
        string? fileName)
    {
        EnsureManage(actor);
        LearningModule? saved = null;
        await _repository.MutateAsync(async db =>
        {
            saved = await SaveCoreAsync(db, input, actor, fileBytes, fileName);
            await Task.CompletedTask;
        });
        return saved!;
    }

    public async Task<LearningModule> SetStatusAsync(string moduleId, string status, HirayaUser actor, bool notify = true)
    {
        EnsureManage(actor);
        if (status is not (LearningModuleStatuses.Draft or LearningModuleStatuses.Published or LearningModuleStatuses.Archived))
            throw new InvalidOperationException("Unknown module status.");

        LearningModule? saved = null;
        await _repository.MutateAsync(async db =>
        {
            var module = Find(db, moduleId) ?? throw new InvalidOperationException("Module was not found.");
            var previous = module.Status;
            module.Status = status;
            module.UpdatedAt = DateTime.UtcNow.ToString("o");
            if (status == LearningModuleStatuses.Published)
                module.PublishedAt = DateTime.UtcNow.ToString("o");
            if (status == LearningModuleStatuses.Archived)
                module.ArchivedAt = DateTime.UtcNow.ToString("o");
            if (status == LearningModuleStatuses.Draft)
                module.ArchivedAt = "";

            var action = previous == LearningModuleStatuses.Archived && status == LearningModuleStatuses.Draft
                ? "restore"
                : status switch
                {
                    LearningModuleStatuses.Published => "publish",
                    LearningModuleStatuses.Archived => "archive",
                    _ => "unpublish"
                };
            AuditService.Append(db, actor, AuditModules.LearningModules, action, "module", module.ModuleId,
                $"{actor.Fullname} set “{module.Title}” to {LearningModuleStatuses.Label(status)}.");

            if (notify && status == LearningModuleStatuses.Published && previous != LearningModuleStatuses.Published)
                NotifyTeachers(db, module, "New Learning Module Available",
                    $"“{module.Title}” has been published for your assigned class.");

            saved = module;
            await Task.CompletedTask;
        });
        return saved!;
    }

    public async Task RestoreAsync(string moduleId, HirayaUser actor) =>
        await SetStatusAsync(moduleId, LearningModuleStatuses.Draft, actor, notify: false);

    public async Task DeleteAsync(string moduleId, HirayaUser actor)
    {
        EnsureManage(actor);
        await _repository.MutateAsync(async db =>
        {
            var module = Find(db, moduleId) ?? throw new InvalidOperationException("Module was not found.");
            if (module.Status == LearningModuleStatuses.Published || !string.IsNullOrWhiteSpace(module.PublishedAt))
                throw new InvalidOperationException("Published modules cannot be deleted. Archive them instead so teachers keep history.");

            db.LearningModuleVersions.RemoveAll(v => v.ModuleId == moduleId);
            db.LearningModules.RemoveAll(m => m.ModuleId == moduleId);
            _storage.DeleteFolder(moduleId);
            AuditService.Append(db, actor, AuditModules.LearningModules, "delete", "module", moduleId,
                $"{actor.Fullname} deleted draft module “{module.Title}”.");
            await Task.CompletedTask;
        });
    }

    public async Task<LearningModuleFile> OpenFileAsync(string moduleId, HirayaUser actor, string? versionId = null, bool auditDownload = true)
    {
        var db = await _repository.LoadAsync();
        var module = Find(db, moduleId) ?? throw new InvalidOperationException("Module was not found.");
        if (!CanAccess(db, actor, module))
            throw new UnauthorizedAccessException("You are not allowed to open that module.");

        var version = db.LearningModuleVersions
            .Where(v => v.ModuleId == moduleId)
            .FirstOrDefault(v => string.IsNullOrWhiteSpace(versionId) ? v.IsCurrent : v.VersionId == versionId)
            ?? db.LearningModuleVersions.FirstOrDefault(v => v.ModuleId == moduleId);

        var relative = version?.FilePath ?? module.FilePath;
        var bytes = _storage.Read(relative) ?? throw new InvalidOperationException("The file is not available.");
        var name = version?.FileName ?? module.FileName;
        var ext = Path.GetExtension(name);

        if (auditDownload)
        {
            await _repository.MutateAsync(async data =>
            {
                AuditService.Append(data, actor, AuditModules.LearningModules, "download", "module", moduleId,
                    $"{actor.Fullname} downloaded “{module.Title}” {version?.Version ?? module.Version}.");
                await Task.CompletedTask;
            });
        }

        return new LearningModuleFile
        {
            Bytes = bytes,
            FileName = name,
            ContentType = LearningModuleRules.ContentType(ext)
        };
    }

    private async Task<LearningModule> SaveCoreAsync(
        HirayaDatabase db,
        LearningModuleSaveRequest input,
        HirayaUser actor,
        byte[]? fileBytes,
        string? fileName)
    {
        var title = input.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Module title is required.");

        var isNew = string.IsNullOrWhiteSpace(input.ModuleId);
        var module = isNew
            ? new LearningModule { ModuleId = IdFactory.New("mod"), CreatedAt = DateTime.UtcNow.ToString("o") }
            : Find(db, input.ModuleId) ?? throw new InvalidOperationException("Module was not found.");

        if (isNew && (fileBytes is null || fileBytes.Length == 0))
            throw new InvalidOperationException("Upload a module file.");

        if (isNew && db.LearningModules.Any(m =>
                string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase) &&
                m.Status != LearningModuleStatuses.Archived &&
                string.Equals(m.ProgramId, input.ProgramId ?? "", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A module with that title already exists for the selected program.");

        var now = DateTime.UtcNow.ToString("o");
        module.Title = title;
        module.Description = input.Description.Trim();
        module.ProgramId = input.ProgramId?.Trim() ?? "";
        module.Subject = input.Subject.Trim();
        module.GradeLevel = input.GradeLevel.Trim();
        module.ModuleType = string.IsNullOrWhiteSpace(input.ModuleType) ? "Lesson guide" : input.ModuleType.Trim();
        module.ClassIds = input.ClassIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        module.UpdatedAt = now;
        if (isNew)
            module.UploadedByUserId = actor.UserId;

        var fileChanged = fileBytes is { Length: > 0 };
        if (fileChanged)
            await ApplyFileAsync(db, module, actor, fileBytes!, fileName ?? "module.bin", input);

        if (!fileChanged && !isNew && !string.IsNullOrWhiteSpace(input.ChangeDescription))
        {
            module.Version = NextVersion(module.Version, major: false);
            var current = db.LearningModuleVersions.FirstOrDefault(v => v.VersionId == module.CurrentVersionId);
            if (current != null)
            {
                current.IsCurrent = false;
                var copy = CloneVersion(current, module.ModuleId, module.Version, input.ChangeDescription, actor.UserId, now);
                copy.IsCurrent = true;
                db.LearningModuleVersions.Add(copy);
                module.CurrentVersionId = copy.VersionId;
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Version))
            module.Version = NormalizeVersion(input.Version);

        if (input.Publish)
        {
            module.Status = LearningModuleStatuses.Published;
            module.PublishedAt = now;
        }
        else if (isNew)
            module.Status = LearningModuleStatuses.Draft;

        if (isNew)
            db.LearningModules.Add(module);

        foreach (var version in db.LearningModuleVersions.Where(v => v.ModuleId == module.ModuleId))
            version.IsCurrent = version.VersionId == module.CurrentVersionId;

        AuditService.Append(db, actor, AuditModules.LearningModules, isNew ? "create" : "update", "module", module.ModuleId,
            isNew
                ? $"{actor.Fullname} uploaded “{module.Title}”."
                : $"{actor.Fullname} updated “{module.Title}” to {module.Version}.");

        if (input.NotifyTeachers && module.Status == LearningModuleStatuses.Published)
        {
            NotifyTeachers(db, module,
                isNew || fileChanged ? "New Learning Module Available" : "Learning Module Updated",
                $"“{module.Title}” ({module.Version}) is available for your assigned class.");
        }

        return module;
    }

    private async Task ApplyFileAsync(
        HirayaDatabase db,
        LearningModule module,
        HirayaUser actor,
        byte[] bytes,
        string fileName,
        LearningModuleSaveRequest input)
    {
        var allowed = LearningModuleRules.AllowedExtensions(db.Settings);
        var ext = Path.GetExtension(LearningModuleRules.SafeFileName(fileName)).ToLowerInvariant();
        if (LearningModuleRules.BlockedExtensions.Contains(ext))
            throw new InvalidOperationException("Executable or script files cannot be uploaded.");
        if (!allowed.Contains(ext))
            throw new InvalidOperationException($"Allowed file types: {string.Join(", ", allowed)}.");
        if (bytes.Length == 0 || bytes.Length > LearningModuleRules.MaxBytes(db.Settings))
            throw new InvalidOperationException($"File must be {LearningModuleRules.MaxBytes(db.Settings) / (1024 * 1024)} MB or smaller.");

        var versionLabel = string.IsNullOrWhiteSpace(input.Version)
            ? (string.IsNullOrWhiteSpace(module.Version) ? "v1.0" : NextVersion(module.Version, major: false))
            : NormalizeVersion(input.Version);
        if (string.IsNullOrWhiteSpace(module.CurrentVersionId))
            versionLabel = string.IsNullOrWhiteSpace(input.Version) ? "v1.0" : NormalizeVersion(input.Version);

        var versionId = IdFactory.New("mver");
        var relative = await _storage.SaveAsync(module.ModuleId, versionId, fileName, bytes);
        foreach (var old in db.LearningModuleVersions.Where(v => v.ModuleId == module.ModuleId))
            old.IsCurrent = false;

        var change = string.IsNullOrWhiteSpace(input.ChangeDescription)
            ? (string.IsNullOrWhiteSpace(module.CurrentVersionId) ? "Initial module" : "Updated file")
            : input.ChangeDescription.Trim();

        db.LearningModuleVersions.Add(new LearningModuleVersion
        {
            VersionId = versionId,
            ModuleId = module.ModuleId,
            Version = versionLabel,
            FileName = LearningModuleRules.SafeFileName(fileName),
            FilePath = relative,
            FileType = ext,
            FileSize = bytes.Length,
            ChangeDescription = change,
            UploadedByUserId = actor.UserId,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            IsCurrent = true
        });

        module.CurrentVersionId = versionId;
        module.Version = versionLabel;
        module.FileName = LearningModuleRules.SafeFileName(fileName);
        module.FilePath = relative;
        module.FileType = ext;
        module.FileSize = bytes.Length;
    }

    private static LearningModuleVersion CloneVersion(
        LearningModuleVersion current,
        string moduleId,
        string version,
        string change,
        string actorId,
        string now) =>
        new()
        {
            VersionId = IdFactory.New("mver"),
            ModuleId = moduleId,
            Version = version,
            FileName = current.FileName,
            FilePath = current.FilePath,
            FileType = current.FileType,
            FileSize = current.FileSize,
            ChangeDescription = change.Trim(),
            UploadedByUserId = actorId,
            CreatedAt = now,
            IsCurrent = true
        };

    private static void NotifyTeachers(HirayaDatabase db, LearningModule module, string title, string message)
    {
        var teacherIds = db.Classes
            .Where(c => IsAssignedToTeacher(module, [c.ClassId], string.IsNullOrWhiteSpace(c.ProgramId) ? [] : [c.ProgramId]))
            .Select(c => c.TeacherId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        if (teacherIds.Count == 0 && (module.ClassIds.Count == 0 && string.IsNullOrWhiteSpace(module.ProgramId)))
        {
            teacherIds = db.Users.Where(u => u.Role == UserRoles.Teacher && u.Status == "active")
                .Select(u => u.UserId).ToList();
        }

        NotificationService.PushToUsers(db, teacherIds, message, NotificationKinds.LearningModule, title,
            $"modules/{module.ModuleId}");
    }

    private static bool IsAssignedToTeacher(LearningModule module, HashSet<string> classIds, HashSet<string> programIds)
    {
        if (module.ClassIds.Count > 0)
            return module.ClassIds.Any(classIds.Contains);
        if (!string.IsNullOrWhiteSpace(module.ProgramId))
            return programIds.Contains(module.ProgramId);
        return true;
    }

    private static void EnsureManage(HirayaUser actor)
    {
        if (!Navigation.CanManageLearningModules(actor.Role))
            throw new UnauthorizedAccessException("Only administrators can manage learning modules.");
    }

    private static string NormalizeVersion(string value)
    {
        var text = value.Trim();
        if (!text.StartsWith('v') && !text.StartsWith('V'))
            text = "v" + text;
        return text;
    }

    private static string NextVersion(string current, bool major)
    {
        var text = (current ?? "v1.0").Trim().TrimStart('v', 'V');
        var parts = text.Split('.');
        if (!int.TryParse(parts[0], out var maj))
            maj = 1;
        var min = 0;
        if (parts.Length > 1)
            int.TryParse(parts[1], out min);
        if (major)
            return $"v{maj + 1}.0";
        return $"v{maj}.{min + 1}";
    }
}
