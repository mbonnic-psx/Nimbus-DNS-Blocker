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
            builder.Services.AddSingleton<PresetService>();
            builder.Services.AddSingleton<CustomSitesService>();
            builder.Services.AddSingleton<IHostsFileService, HostsFileService>();
            return builder.Build();
        }
    }
}
