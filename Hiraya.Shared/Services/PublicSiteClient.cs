using System.Net.Http.Json;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Auth;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class PublicSiteClient
{
    private readonly IHttpClientFactory _http;

    public PublicSiteClient(IHttpClientFactory http) => _http = http;

    public async Task<PublicSiteDto> GetSiteAsync(CancellationToken ct = default)
    {
        var client = _http.CreateClient("hiraya-api");
        var site = await client.GetFromJsonAsync<PublicSiteDto>("api/public/site", JsonDefaults.Options, ct);
        return site ?? new PublicSiteDto();
    }

    public async Task<string> SubmitEnrollmentAsync(PublicEnrollmentRequest request, CancellationToken ct = default)
    {
        var client = _http.CreateClient("hiraya-api");
        using var response = await client.PostAsJsonAsync("api/public/enrollment", request, JsonDefaults.Options, ct);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonDefaults.Options, ct);
            throw new InvalidOperationException(problem?.Error ?? "The application could not be submitted.");
        }

        var result = await response.Content.ReadFromJsonAsync<PublicEnrollmentResult>(JsonDefaults.Options, ct);
        if (result is null || string.IsNullOrWhiteSpace(result.ApplicationId))
            throw new InvalidOperationException("The application was accepted but no reference was returned.");
        return result.ApplicationId;
    }

    public async Task<HirayaUser?> LoginAsync(string login, string password, CancellationToken ct = default)
    {
        var client = _http.CreateClient("hiraya-api");
        using var response = await client.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest { Login = login, Password = password },
            JsonDefaults.Options,
            ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;
        if (response.StatusCode == System.Net.HttpStatusCode.Locked)
            throw new InvalidOperationException("Too many failed sign-ins. Wait 15 minutes and try again.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HirayaUser>(JsonDefaults.Options, ct);
    }

    private sealed class ErrorBody
    {
        public string? Error { get; set; }
    }
}
