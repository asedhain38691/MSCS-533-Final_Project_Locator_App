using Microsoft.Maui.Controls.Maps;
using Microsoft.Extensions.Logging;
using MauiHeatMap.Data;

namespace MauiHeatMap;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiMaps()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "locations.db3");
		builder.Services.AddSingleton(_ => new LocationDb(dbPath));
		return builder.Build();
	}
}
