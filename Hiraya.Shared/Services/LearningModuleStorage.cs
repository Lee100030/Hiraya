using Hiraya.Shared;
using Hiraya.Shared.Models;

namespace Hiraya.Shared.Services;

public static class LearningModuleRules
{
    public static readonly string[] DefaultExtensions =
    [
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    ];

    public static readonly string[] BlockedExtensions =
    [
        ".exe", ".bat", ".cmd", ".com", ".msi", ".dll", ".js", ".vbs", ".ps1", ".sh", ".scr", ".jar"
    ];

    public const int DefaultMaxMb = 25;

    public static HashSet<string> AllowedExtensions(CenterSettings settings)
    {
        var raw = settings.ModuleAllowedExtensions;
        var parts = string.IsNullOrWhiteSpace(raw)
            ? DefaultExtensions
            : raw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant())
                .Select(x => x.StartsWith('.') ? x : "." + x);
        return parts.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static long MaxBytes(CenterSettings settings)
    {
        var mb = settings.ModuleMaxFileMb > 0 ? settings.ModuleMaxFileMb : DefaultMaxMb;
        return Math.Clamp(mb, 1, 100) * 1024L * 1024L;
    }

    public static string SafeFileName(string? name)
    {
        var file = Path.GetFileName(name ?? "").Replace("..", "", StringComparison.Ordinal);
        foreach (var c in Path.GetInvalidFileNameChars())
            file = file.Replace(c, '_');
        if (string.IsNullOrWhiteSpace(file))
            return "module.bin";
        return file.Length <= 180 ? file : file[..180];
    }

    public static string ContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };

    public static bool CanPreviewInline(string? fileType) =>
        fileType is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";
}

public class LearningModuleStorage
{
    private readonly IHirayaAppPaths _paths;

    public LearningModuleStorage(IHirayaAppPaths paths) => _paths = paths;

    public string Root => Path.Combine(_paths.DataDirectory, "media", "modules");

    public async Task<string> SaveAsync(string moduleId, string versionId, string fileName, byte[] bytes)
    {
        var safe = LearningModuleRules.SafeFileName(fileName);
        var folder = Path.Combine(Root, Sanitize(moduleId), Sanitize(versionId));
        Directory.CreateDirectory(folder);
        var full = Path.Combine(folder, safe);
        await File.WriteAllBytesAsync(full, bytes);
        return Path.Combine("media", "modules", Sanitize(moduleId), Sanitize(versionId), safe).Replace('\\', '/');
    }

    public string FullPath(string relative)
    {
        var cleaned = relative.Replace('\\', '/').TrimStart('/');
        if (cleaned.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file path.");
        return Path.GetFullPath(Path.Combine(_paths.DataDirectory, cleaned.Replace('/', Path.DirectorySeparatorChar)));
    }

    public byte[]? Read(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return null;
        var full = FullPath(relative);
        var root = Path.GetFullPath(Root);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            return null;
        return File.ReadAllBytes(full);
    }

    public void DeleteFolder(string moduleId)
    {
        var folder = Path.Combine(Root, Sanitize(moduleId));
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
    }

    private static string Sanitize(string id)
    {
        var value = string.Concat((id ?? "").Where(c => char.IsLetterOrDigit(c) || c is '_' or '-'));
        return string.IsNullOrWhiteSpace(value) ? "file" : value;
    }
}
