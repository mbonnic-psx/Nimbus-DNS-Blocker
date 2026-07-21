using Microsoft.Extensions.Logging;
using Nimbus_Internet_Blocker.Services;

namespace Nimbus_Internet_Blocker
{
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

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<ISnackbarService, SnackbarService>();
            builder.Services.AddSingleton<QuoteService>();
            builder.Services.AddSingleton<IPresetService, PresetService>();
            builder.Services.AddSingleton<ICustomSitesService, CustomSitesService>();
#pragma warning disable CA1416 // BrowserPolicyService/HostsFileService are intentionally Windows-only
            builder.Services.AddSingleton<IBrowserPolicyService, BrowserPolicyService>();
            builder.Services.AddSingleton<IHostsFileService, HostsFileService>();
#pragma warning restore CA1416
            builder.Services.AddSingleton<IPasswordService, PasswordService>();
            return builder.Build();
        }
    }
}
