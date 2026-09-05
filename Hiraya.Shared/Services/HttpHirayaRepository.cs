using System.Net.Http.Json;
using System.Text.Json;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;
using Microsoft.Extensions.Logging;

namespace Hiraya.Shared.Services;

public class HttpHirayaRepository : IHirayaRepository
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<HttpHirayaRepository> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HirayaDatabase? _cache;

    public HttpHirayaRepository(
        IHttpClientFactory http,
        ILogger<HttpHirayaRepository> logger)
    {
        _http = http;
        _logger = logger;
    }

    public bool UsingFirebase => false;

    public async Task MutateAsync(Func<HirayaDatabase, Task> mutator, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var db = await LoadUnlockedAsync(ct);
            await mutator(db);
            await SaveUnlockedAsync(db, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HirayaDatabase> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await LoadUnlockedAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(HirayaDatabase database, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await SaveUnlockedAsync(database, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetItemAsync<T>(string collection, string id, T item, CancellationToken ct = default) =>
        MutateAsync(_ => Task.CompletedTask, ct);

    public Task DeleteItemAsync(string collection, string id, CancellationToken ct = default) =>
        MutateAsync(_ => Task.CompletedTask, ct);

    private async Task<HirayaDatabase> LoadUnlockedAsync(CancellationToken ct)
    {
        var client = _http.CreateClient("hiraya-api");
        using var response = await client.GetAsync("api/store", ct);
        await EnsureSuccess(response, "load");
        var db = await response.Content.ReadFromJsonAsync<HirayaDatabase>(JsonDefaults.Options, ct)
                 ?? new HirayaDatabase();
        _cache = Clone(db);
        return Clone(_cache);
    }

    private async Task SaveUnlockedAsync(HirayaDatabase database, CancellationToken ct)
    {
        var client = _http.CreateClient("hiraya-api");
        using var response = await client.PutAsJsonAsync("api/store", database, JsonDefaults.Options, ct);
        await EnsureSuccess(response, "save");
        _cache = Clone(database);
    }

    private async Task EnsureSuccess(HttpResponseMessage response, string action)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync();
        _logger.LogError("Hiraya API {Action} failed: {Status} {Body}", action, (int)response.StatusCode, body);
        throw new InvalidOperationException(Friendly(body, response.StatusCode));
    }

    private static string Friendly(string body, System.Net.HttpStatusCode status)
    {
        if (status == System.Net.HttpStatusCode.ServiceUnavailable ||
            body.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("MySQL", StringComparison.OrdinalIgnoreCase))
            return "The learning center database is not available. Start XAMPP MySQL and the HLCMS API, then try again.";
        return "The server could not complete that request. Try again, or ask an administrator to check the API log.";
    }

    private static HirayaDatabase Clone(HirayaDatabase db)
    {
        var json = JsonSerializer.Serialize(db, JsonDefaults.Options);
        return JsonSerializer.Deserialize<HirayaDatabase>(json, JsonDefaults.Options) ?? new HirayaDatabase();
    }
}
