using System.Net.Http.Json;
using Hiraya.Shared;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;
using Microsoft.Extensions.Options;

namespace Hiraya.Shared.Services;

public class LearningModuleClient
{
    private readonly LearningModuleService _local;
    private readonly IHirayaRepository _repository;
    private readonly IHttpClientFactory _http;
    private readonly HirayaApiOptions _api;

    public LearningModuleClient(
        LearningModuleService local,
        IHirayaRepository repository,
        IHttpClientFactory http,
        IOptions<HirayaApiOptions> api)
    {
        _local = local;
        _repository = repository;
        _http = http;
        _api = api.Value;
    }

    public bool UseRemote => _api.UseRemoteStore;

    public async Task<List<LearningModule>> ListAsync(HirayaUser actor, bool includeHidden = false)
    {
        if (!UseRemote)
        {
            var db = await _repository.LoadAsync();
            return LearningModuleService.Visible(db, actor, includeHidden).ToList();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/modules?hidden={includeHidden}");
        AddActor(request, actor);
        using var response = await Send(request);
        await EnsureOk(response);
        return await response.Content.ReadFromJsonAsync<List<LearningModule>>(JsonDefaults.Options) ?? [];
    }

    public async Task<LearningModule?> GetAsync(string moduleId, HirayaUser actor)
    {
        if (!UseRemote)
        {
            var db = await _repository.LoadAsync();
            var module = LearningModuleService.Find(db, moduleId);
            if (module is null)
                return null;
            if (Navigation.CanManageLearningModules(actor.Role))
                return module;
            return LearningModuleService.CanAccess(db, actor, module) ? module : null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/modules/{Uri.EscapeDataString(moduleId)}");
        AddActor(request, actor);
        using var response = await Send(request);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
            return null;
        await EnsureOk(response);
        return await response.Content.ReadFromJsonAsync<LearningModule>(JsonDefaults.Options);
    }

    public async Task<List<LearningModuleVersion>> VersionsAsync(string moduleId, HirayaUser actor)
    {
        if (!UseRemote)
        {
            var db = await _repository.LoadAsync();
            var module = LearningModuleService.Find(db, moduleId);
            if (module is null)
                return [];
            if (!Navigation.CanManageLearningModules(actor.Role))
                return [];
            return db.LearningModuleVersions.Where(v => v.ModuleId == moduleId).OrderByDescending(v => v.CreatedAt).ToList();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/modules/{Uri.EscapeDataString(moduleId)}/versions");
        AddActor(request, actor);
        using var response = await Send(request);
        await EnsureOk(response);
        return await response.Content.ReadFromJsonAsync<List<LearningModuleVersion>>(JsonDefaults.Options) ?? [];
    }

    public async Task<LearningModule> SaveAsync(
        LearningModuleSaveRequest input,
        HirayaUser actor,
        byte[]? fileBytes,
        string? fileName,
        IProgress<int>? progress = null)
    {
        progress?.Report(15);
        if (!UseRemote)
        {
            var saved = await _local.SaveAsync(input, actor, fileBytes, fileName);
            progress?.Report(100);
            return saved;
        }

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(input.ModuleId ?? ""), "moduleId");
        content.Add(new StringContent(input.Title ?? ""), "title");
        content.Add(new StringContent(input.Description ?? ""), "description");
        content.Add(new StringContent(input.ProgramId ?? ""), "programId");
        content.Add(new StringContent(input.Subject ?? ""), "subject");
        content.Add(new StringContent(input.GradeLevel ?? ""), "gradeLevel");
        content.Add(new StringContent(input.ModuleType ?? ""), "moduleType");
        content.Add(new StringContent(string.Join(",", input.ClassIds)), "classIds");
        content.Add(new StringContent(input.Version ?? ""), "version");
        content.Add(new StringContent(input.ChangeDescription ?? ""), "changeDescription");
        content.Add(new StringContent(input.Publish ? "true" : "false"), "publish");
        content.Add(new StringContent(input.NotifyTeachers ? "true" : "false"), "notifyTeachers");
        if (fileBytes is { Length: > 0 })
        {
            var file = new ByteArrayContent(fileBytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(file, "file", LearningModuleRules.SafeFileName(fileName));
        }

        progress?.Report(45);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/modules") { Content = content };
        AddActor(request, actor);
        using var response = await Send(request);
        await EnsureOk(response);
        progress?.Report(100);
        return await response.Content.ReadFromJsonAsync<LearningModule>(JsonDefaults.Options)
               ?? throw new InvalidOperationException("The module was saved but no details were returned.");
    }

    public async Task SetStatusAsync(string moduleId, string status, HirayaUser actor)
    {
        if (!UseRemote)
        {
            await _local.SetStatusAsync(moduleId, status, actor);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/modules/{Uri.EscapeDataString(moduleId)}/status")
        {
            Content = JsonContent.Create(new StatusBody { Status = status }, options: JsonDefaults.Options)
        };
        AddActor(request, actor);
        using var response = await Send(request);
        await EnsureOk(response);
    }

    public async Task DeleteAsync(string moduleId, HirayaUser actor)
    {
        if (!UseRemote)
        {
            await _local.DeleteAsync(moduleId, actor);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/modules/{Uri.EscapeDataString(moduleId)}");
        AddActor(request, actor);
        using var response = await Send(request);
        await EnsureOk(response);
    }

    public async Task<LearningModuleFile> OpenFileAsync(string moduleId, HirayaUser actor, string? versionId = null)
    {
        if (!UseRemote)
            return await _local.OpenFileAsync(moduleId, actor, versionId);

        var url = $"api/modules/{Uri.EscapeDataString(moduleId)}/file";
        if (!string.IsNullOrWhiteSpace(versionId))
            url += "?versionId=" + Uri.EscapeDataString(versionId);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddActor(request, actor);
        using var response = await Send(request);
        await EnsureOk(response);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var name = response.Content.Headers.ContentDisposition?.FileNameStar
                   ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                   ?? "module.bin";
        var type = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new LearningModuleFile { Bytes = bytes, FileName = name, ContentType = type };
    }

    public string PreviewUrl(string moduleId, HirayaUser actor, string? versionId = null)
    {
        var root = (_api.BaseUrl ?? "").TrimEnd('/');
        var url = $"{root}/api/modules/{Uri.EscapeDataString(moduleId)}/file?inline=true&actor={Uri.EscapeDataString(actor.UserId)}";
        if (!string.IsNullOrWhiteSpace(versionId))
            url += "&versionId=" + Uri.EscapeDataString(versionId);
        return url;
    }

    private static void AddActor(HttpRequestMessage request, HirayaUser actor) =>
        request.Headers.TryAddWithoutValidation("X-Hiraya-User-Id", actor.UserId);

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
    {
        var client = _http.CreateClient("hiraya-api");
        return await client.SendAsync(request);
    }

    private static async Task EnsureOk(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Sign in to access learning modules.");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("You are not allowed to access that learning module.");
        var message = TryError(body) ?? "The learning module request failed.";
        throw new InvalidOperationException(message);
    }

    private static string? TryError(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch
        {
            // plain text
        }

        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private sealed class StatusBody
    {
        public string Status { get; set; } = "";
    }
}
