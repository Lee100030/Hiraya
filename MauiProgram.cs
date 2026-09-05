using Hiraya.Services;
using Hiraya.Shared;
using Hiraya.Shared.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hiraya;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        try
        {
            using var settings = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
            builder.Configuration.AddJsonStream(settings);
        }
        catch (Exception)
        {
            // Packaged settings are optional; defaults stay on local JSON.
        }

        var contentRoot = FileSystem.AppDataDirectory;
        builder.Services.AddSingleton<IHirayaAppPaths>(_ =>
            new DefaultHirayaAppPaths(contentRoot, Path.Combine(contentRoot, ".data")));
        builder.Services.AddHirayaShared(
            options => { options.UseFirebase = false; },
            api =>
            {
                builder.Configuration.GetSection(HirayaApiOptions.SectionName).Bind(api);
                if (DeviceInfo.Platform == DevicePlatform.Android &&
                    (string.IsNullOrWhiteSpace(api.BaseUrl) ||
                     api.BaseUrl.Contains("127.0.0.1", StringComparison.Ordinal) ||
                     api.BaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
                {
                    // Emulator loopback to the Windows host where Hiraya.Api runs.
                    api.BaseUrl = "http://10.0.2.2:5188";
                }
            });
        builder.Services.AddSingleton<SessionService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
