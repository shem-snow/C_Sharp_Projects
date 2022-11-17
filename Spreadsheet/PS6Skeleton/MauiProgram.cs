namespace SpreadsheetGUI;

/// <summary>
/// This class creates the MAUI application which depends on the framework that's completely provided by Visual Studio.
/// All it does is create and build the MAUI App
/// 
/// Last updated by Shem Snow
/// On date: 10/21/2022
/// </summary>
public static class MauiProgram
{
	/// <summary>
	/// This method just creates a Maui Application and selects the fonts for it.
	/// It can set up other configurations as well.
	/// </summary>
	/// <returns>A builder that can create the Maui app</returns>
	public static MauiApp CreateMauiApp()
	{
		// build
        var builder = MauiApp.CreateBuilder();
		builder
			// configure
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		return builder.Build();
	}
}

