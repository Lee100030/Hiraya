using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hiraya.Shared.Services.Firebase;

public class FirebaseCredentialProvider
{
    private readonly FirebaseOptions _options;
    private readonly IHirayaAppPaths _paths;
    private readonly ILogger<FirebaseCredentialProvider> _logger;
    private GoogleCredential? _credential;
    private bool _initialized;

    public FirebaseCredentialProvider(
        IOptions<FirebaseOptions> options,
        IHirayaAppPaths paths,
        ILogger<FirebaseCredentialProvider> logger)
    {
        _options = options.Value;
        _paths = paths;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            EnsureInitialized();
            return _credential != null;
        }
    }

    public string DatabaseUrl => _options.DatabaseUrl.TrimEnd('/');

    public string ProjectId => _options.ProjectId;

    public GoogleCredential? GetCredential()
    {
        EnsureInitialized();
        return _credential;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        EnsureInitialized();
        if (_credential == null) return null;

        var scoped = _credential.CreateScoped(
            "https://www.googleapis.com/auth/firebase.database",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/cloud-platform");

        return await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync();
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (!_options.UseFirebase)
        {
            _logger.LogInformation("Firebase disabled via configuration. Using local store.");
            return;
        }

        try
        {
            var root = _paths.ContentRootPath;
            var pathCandidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(_options.ServiceAccountPath))
            {
                pathCandidates.Add(Path.IsPathRooted(_options.ServiceAccountPath)
                    ? _options.ServiceAccountPath
                    : Path.Combine(root, _options.ServiceAccountPath));
            }

            pathCandidates.Add(Path.Combine(root, "firebase-service-account.json"));

            foreach (var path in pathCandidates.Distinct())
            {
                if (!File.Exists(path)) continue;
#pragma warning disable CS0618
                _credential = GoogleCredential.FromFile(path);
#pragma warning restore CS0618
                _logger.LogInformation("Firebase credentials loaded from {Path}", path);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_options.ClientEmail) &&
                !string.IsNullOrWhiteSpace(_options.PrivateKey) &&
                !_options.PrivateKey.Contains("PASTE_", StringComparison.Ordinal))
            {
                var json = JsonSerializer.Serialize(new
                {
                    type = "service_account",
                    project_id = _options.ProjectId,
                    client_email = _options.ClientEmail,
                    private_key = _options.PrivateKey.Replace("\\n", "\n", StringComparison.Ordinal)
                });

#pragma warning disable CS0618
                _credential = GoogleCredential.FromJson(json);
#pragma warning restore CS0618
                _logger.LogInformation("Firebase credentials loaded from configuration values.");
                return;
            }

            _logger.LogWarning(
                "Firebase is not configured. Place firebase-service-account.json in the app data folder or keep UseFirebase false. Using local store.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Firebase credentials.");
            _credential = null;
        }
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
