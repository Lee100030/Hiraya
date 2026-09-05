using Hiraya.Shared;
using Hiraya.Shared.Services.Auth;
using Hiraya.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHirayaShared(
    _ => { },
    api => builder.Configuration.GetSection(HirayaApiOptions.SectionName).Bind(api));
builder.Services.AddSingleton<IHirayaAppPaths>(_ =>
    new DefaultHirayaAppPaths(builder.Environment.ContentRootPath));
builder.Services.AddScoped<WebSessionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<Hiraya.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
