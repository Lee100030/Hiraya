namespace Hiraya.Shared.Models;

public static class LearningModuleStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";

    public static readonly string[] All = [Draft, Published, Archived];

    public static string Label(string? status) => status switch
    {
        Draft => "Draft",
        Published => "Published",
        Archived => "Archived",
        _ => string.IsNullOrWhiteSpace(status) ? "Draft" : status
    };
}

public static class LearningModuleTypes
{
    public static readonly string[] All =
    [
        "Lesson guide",
        "Worksheet",
        "Presentation",
        "Assessment",
        "Handout",
        "Activity pack",
        "Other"
    ];
}

public class LearningModule
{
    public string ModuleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ProgramId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string GradeLevel { get; set; } = "";
    public string ModuleType { get; set; } = "Lesson guide";
    public List<string> ClassIds { get; set; } = [];
    public string CurrentVersionId { get; set; } = "";
    public string Version { get; set; } = "v1.0";
    public string Status { get; set; } = LearningModuleStatuses.Draft;
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileType { get; set; } = "";
    public long FileSize { get; set; }
    public string UploadedByUserId { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string ArchivedAt { get; set; } = "";
}

public class LearningModuleVersion
{
    public string VersionId { get; set; } = "";
    public string ModuleId { get; set; } = "";
    public string Version { get; set; } = "v1.0";
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileType { get; set; } = "";
    public long FileSize { get; set; }
    public string ChangeDescription { get; set; } = "";
    public string UploadedByUserId { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool IsCurrent { get; set; }
}

public class LearningModuleSaveRequest
{
    public string ModuleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ProgramId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string GradeLevel { get; set; } = "";
    public string ModuleType { get; set; } = "Lesson guide";
    public List<string> ClassIds { get; set; } = [];
    public string Version { get; set; } = "";
    public string ChangeDescription { get; set; } = "";
    public bool Publish { get; set; }
    public bool NotifyTeachers { get; set; } = true;
}
