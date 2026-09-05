using System.Collections.Concurrent;
using System.Net.Http.Json;
using Hiraya.Shared.Models;
using Hiraya.Shared.Security;
using Hiraya.Shared;
using Hiraya.Shared.Services.Firebase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hiraya.Shared.Services.Auth;

public class PortalAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Failures = new(StringComparer.OrdinalIgnoreCase);

    private readonly IHirayaRepository _repository;
    private readonly IHttpClientFactory _http;
    private readonly HirayaApiOptions _api;

    public PortalAuthService(
        IHirayaRepository repository,
        IHttpClientFactory http,
        IOptions<HirayaApiOptions> api)
    {
        _repository = repository;
        _http = http;
        _api = api.Value;
    }

    public async Task<HirayaUser?> AuthenticateAsync(string login, string password)
    {
        var key = login.Trim();
        if (IsLocked(key))
            throw new InvalidOperationException("Too many failed sign-ins. Wait 15 minutes and try again.");

        if (_api.UseRemoteStore)
            return await AuthenticateRemoteAsync(key, password);

        return await AuthenticateLocalAsync(key, password);
    }

    private async Task<HirayaUser?> AuthenticateRemoteAsync(string key, string password)
    {
        var client = _http.CreateClient("hiraya-api");
        using var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Login = key, Password = password });
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            RegisterFailure(key);
            return null;
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Locked)
            throw new InvalidOperationException("Too many failed sign-ins. Wait 15 minutes and try again.");
        response.EnsureSuccessStatusCode();
        Failures.TryRemove(key, out _);
        return await response.Content.ReadFromJsonAsync<HirayaUser>(JsonDefaults.Options);
    }

    private async Task<HirayaUser?> AuthenticateLocalAsync(string key, string password)
    {
        var db = await _repository.LoadAsync();
        var user = db.Users.FirstOrDefault(u =>
            (string.Equals(u.Email, key, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(u.Username, key, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(u.Fullname, key, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(u.Status, "active", StringComparison.OrdinalIgnoreCase));

        if (user == null || !PasswordHasher.Verify(password, user.Password))
        {
            RegisterFailure(key);
            return null;
        }

        Failures.TryRemove(key, out _);

        await _repository.MutateAsync(async data =>
        {
            var live = data.Users.FirstOrDefault(u => u.UserId == user.UserId);
            if (live == null)
                return;
            if (!PasswordHasher.IsHashed(live.Password))
                live.Password = PasswordHasher.Hash(password);
            live.LastLoginAt = DateTime.UtcNow.ToString("o");
            await Task.CompletedTask;
        });

        var fresh = (await _repository.LoadAsync()).Users.First(u => u.UserId == user.UserId);
        return AccountService.ClonePublic(fresh);
    }

    private static bool IsLocked(string login)
    {
        if (!Failures.TryGetValue(login, out var state))
            return false;
        if (DateTime.UtcNow - state.WindowStart > LockoutWindow)
        {
            Failures.TryRemove(login, out _);
            return false;
        }

        return state.Count >= MaxFailedAttempts;
    }

    private static void RegisterFailure(string login)
    {
        Failures.AddOrUpdate(
            login,
            _ => (1, DateTime.UtcNow),
            (_, state) => DateTime.UtcNow - state.WindowStart > LockoutWindow
                ? (1, DateTime.UtcNow)
                : (state.Count + 1, state.WindowStart));
    }
}

public static class HirayaServiceCollectionExtensions
{
    public static IServiceCollection AddHirayaShared(
        this IServiceCollection services,
        Action<FirebaseOptions>? configureFirebase = null,
        Action<HirayaApiOptions>? configureApi = null)
    {
        if (configureFirebase != null)
            services.Configure(configureFirebase);
        else
            services.Configure<FirebaseOptions>(_ => { });

        if (configureApi != null)
            services.Configure(configureApi);
        else
            services.Configure<HirayaApiOptions>(_ => { });

        services.AddHttpClient("firebase-rtdb");
        services.AddHttpClient("hiraya-api", (sp, client) =>
        {
            var api = sp.GetRequiredService<IOptions<HirayaApiOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(api.BaseUrl))
                client.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddSingleton<FirebaseCredentialProvider>();
        services.AddSingleton<IHirayaRepository>(sp =>
        {
            var api = sp.GetRequiredService<IOptions<HirayaApiOptions>>().Value;
            if (api.UseRemoteStore)
                return ActivatorUtilities.CreateInstance<HttpHirayaRepository>(sp);
            return ActivatorUtilities.CreateInstance<HirayaRepository>(sp);
        });
        services.AddSingleton<PortalAuthService>();
        services.AddSingleton<EnrollmentService>();
        services.AddSingleton<ClassService>();
        services.AddSingleton<AttendanceService>();
        services.AddSingleton<ScheduleService>();
        services.AddSingleton<ProgressService>();
        services.AddSingleton<PaymentService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<AuditService>();
        services.AddSingleton<StaffService>();
        services.AddSingleton<AccountService>();
        services.AddSingleton<ProfilePhotoService>();
        services.AddSingleton<UserAdminService>();
        services.AddSingleton<CenterSettingsService>();
        services.AddSingleton<ProgramService>();
        services.AddSingleton<LearningModuleStorage>();
        services.AddSingleton<LearningModuleService>();
        services.AddSingleton<LearningModuleClient>();
        services.AddSingleton<PublicSiteClient>();
        return services;
    }
}
