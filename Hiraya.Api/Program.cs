using Hiraya.Api;
using Hiraya.Data;
using Hiraya.Shared;
using Hiraya.Shared.Models;
using Hiraya.Shared.Services;
using Hiraya.Shared.Services.Auth;
using Hiraya.Shared.Services.Firebase;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var password = Environment.GetEnvironmentVariable("HIRAYA_MYSQL_PASSWORD");
var connection = builder.Configuration.GetConnectionString("Hiraya")
                 ?? "Server=127.0.0.1;Port=3306;Database=hiraya_learning_center;User=root;Password=";
if (!string.IsNullOrEmpty(password))
    connection = ReplacePassword(connection, password);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddDbContextFactory<HirayaLearningCenterDbContext>(options =>
{
    options.UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 21)));
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100L * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100L * 1024 * 1024;
});
builder.Services.AddSingleton<IHirayaAppPaths>(_ =>
    new DefaultHirayaAppPaths(builder.Environment.ContentRootPath));
builder.Services.AddHttpClient("hiraya-api");
builder.Services.Configure<Hiraya.Shared.HirayaApiOptions>(_ => { });
builder.Services.AddSingleton<IHirayaRepository, EfHirayaRepository>();
builder.Services.AddSingleton<PortalAuthService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<EnrollmentService>();
builder.Services.AddSingleton<LearningModuleStorage>();
builder.Services.AddSingleton<LearningModuleService>();

var app = builder.Build();
app.UseCors();
app.UseExceptionHandler(err =>
{
    err.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "The learning center database is not available. Start XAMPP MySQL and confirm the connection string."
        });
    });
});

app.MapGet("/api/health", async (IDbContextFactory<HirayaLearningCenterDbContext> factory) =>
{
    try
    {
        await using var db = await factory.CreateDbContextAsync();
        var ok = await db.Database.CanConnectAsync();
        return ok
            ? Results.Ok(new { status = "ok", database = "hiraya_learning_center" })
            : Results.Json(new { status = "down", database = "hiraya_learning_center" }, statusCode: 503);
    }
    catch
    {
        return Results.Json(new { status = "down", database = "hiraya_learning_center" }, statusCode: 503);
    }
});

app.MapGet("/api/store", async (IHirayaRepository repo, CancellationToken ct) =>
{
    var data = await repo.LoadAsync(ct);
    return Results.Json(StoreSanitizer.PublicCopy(data), JsonDefaults.Options);
});

app.MapPost("/api/auth/login", async (LoginRequest body, PortalAuthService auth) =>
{
    try
    {
        var user = await auth.AuthenticateAsync(body.Login, body.Password);
        return user is null ? Results.Unauthorized() : Results.Json(user, JsonDefaults.Options);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status423Locked);
    }
});

app.MapPost("/api/auth/password", async (PasswordChangeRequest body, AccountService accounts) =>
{
    try
    {
        await accounts.ChangePasswordAsync(body.UserId, body.CurrentPassword, body.NewPassword, body.ConfirmPassword);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Text(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapGet("/api/public/site", async (IHirayaRepository repo, CancellationToken ct) =>
{
    var data = await repo.LoadAsync(ct);
    return Results.Json(PublicSiteCatalog.From(data), JsonDefaults.Options);
});

app.MapPost("/api/public/enrollment", async (PublicEnrollmentRequest body, EnrollmentService enroll) =>
{
    try
    {
        var id = await enroll.SubmitPublicApplicationAsync(
            body.StudentFullname,
            body.StudentBirthdate,
            body.StudentGender,
            body.PreferredProgram,
            body.ParentFullname,
            body.ParentEmail,
            body.ParentPhone);
        return Results.Json(new PublicEnrollmentResult { ApplicationId = id }, JsonDefaults.Options);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPut("/api/store", async (HirayaDatabase incoming, IHirayaRepository repo, CancellationToken ct) =>
{
    await repo.SaveAsync(incoming, ct);
    return Results.NoContent();
});

LearningModuleApi.Map(app);

try
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HirayaLearningCenterDbContext>>();
    await using var context = await db.CreateDbContextAsync();
    await DatabaseInitializer.InitializeAsync(context, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "MySQL initialization failed. Start XAMPP MySQL and retry.");
}

app.Run();

static string ReplacePassword(string connection, string password)
{
    var parts = connection.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase)
            ? "Password=" + password
            : p)
        .ToList();
    if (!parts.Any(p => p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase)))
        parts.Add("Password=" + password);
    return string.Join(";", parts) + ";";
}
