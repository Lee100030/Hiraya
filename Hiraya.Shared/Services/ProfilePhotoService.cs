using Hiraya.Shared;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class ProfilePhotoService
{
    public const long MaxBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".webp", ".gif", ".bmp"
    };

    private readonly IHirayaAppPaths _paths;
    private readonly AccountService _accounts;

    public ProfilePhotoService(IHirayaAppPaths paths, AccountService accounts)
    {
        _paths = paths;
        _accounts = accounts;
    }

    public string Root => Path.Combine(_paths.DataDirectory, "media", "profiles");

    public async Task<string> SaveAsync(string userId, string fileName, Stream content, long length)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        return await SaveAsync(userId, fileName, ms.ToArray());
    }

    public async Task<string> SaveAsync(string userId, string fileName, byte[] bytes)
    {
        if (bytes.Length == 0)
            throw new InvalidOperationException("The selected file was empty.");
        if (bytes.Length > MaxBytes)
            throw new InvalidOperationException("Image must be 8 MB or smaller. Compress or crop the photo and try again.");

        var ext = ResolveExtension(fileName, bytes);
        Directory.CreateDirectory(Root);
        var relative = Path.Combine("media", "profiles", $"{userId}{ext}");
        var full = Path.Combine(_paths.DataDirectory, relative);
        foreach (var leftover in Directory.GetFiles(Root, userId + ".*"))
        {
            try { File.Delete(leftover); } catch { /* ignore locked files */ }
        }

        await File.WriteAllBytesAsync(full, bytes);
        var stored = relative.Replace('\\', '/');
        await _accounts.SetProfileImageAsync(userId, stored);
        return stored;
    }

    public async Task RemoveAsync(string userId)
    {
        if (Directory.Exists(Root))
        {
            foreach (var leftover in Directory.GetFiles(Root, userId + ".*"))
            {
                try { File.Delete(leftover); } catch { /* ignore */ }
            }
        }
        await _accounts.SetProfileImageAsync(userId, "");
    }

    public string? ToDataUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;
        var full = Path.Combine(_paths.DataDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
            return null;
        return ToDataUrl(File.ReadAllBytes(full), full);
    }

    public static string ToDataUrl(byte[] bytes, string fileName)
    {
        var ext = ResolveExtension(fileName, bytes);
        var mime = MimeFromExtension(ext);
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    public static string ResolveExtension(string? fileName, byte[] bytes)
    {
        var sniffed = SniffExtension(bytes);
        if (sniffed is not null)
            return sniffed;

        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (ext is ".jpeg" or ".jpe" or ".jfif")
            ext = ".jpg";
        if (Allowed.Contains(ext))
            return ext;

        throw new InvalidOperationException(
            "That photo format is not supported. Use a JPG, PNG, or WEBP file (not HEIC).");
    }

    private static string MimeFromExtension(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };

    private static string? SniffExtension(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ".jpg";
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return ".png";
        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return ".webp";
        if (bytes.Length >= 6 &&
            bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
            return ".gif";
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
            return ".bmp";
        if (IsHeif(bytes))
            throw new InvalidOperationException(
                "iPhone HEIC photos are not supported here. Open the photo and export it as JPG, then upload again.");
        return null;
    }

    private static bool IsHeif(byte[] bytes)
    {
        if (bytes.Length < 12)
            return false;
        if (bytes[4] != (byte)'f' || bytes[5] != (byte)'t' || bytes[6] != (byte)'y' || bytes[7] != (byte)'p')
            return false;
        var brand = System.Text.Encoding.ASCII.GetString(bytes, 8, Math.Min(4, bytes.Length - 8));
        return brand is "heic" or "heif" or "mif1" or "msf1" or "heix";
    }
}
