using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eu.nerdfactor.WOLProxy;

/// <summary>
/// Main application.
/// </summary>
public static class App {

	/// <summary>
	/// Main entry point of the application.
	/// </summary>
	/// <param name="args"></param>
	public static void Main(string[] args) {
		CreateHostBuilder(args).Build().Run();
	}

	/// <summary>
	/// Builds the application host.
	/// Handles loading of configuration from local json or xml files. The path
	/// to the configuration file can be passed as the first application argument.
	/// </summary>
	/// <param name="args"></param>
	/// <returns></returns>
	private static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureAppConfiguration(
				(_, config) => {
					if (args.Length > 0 && !string.IsNullOrEmpty(args[0]) && File.Exists(args[0])) {
						config.AddJsonFile(args[0], optional: true, reloadOnChange: false);
						config.AddXmlFile(args[0], optional: true, reloadOnChange: false);
					} else {
						config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
						config.AddXmlFile("appsettings.xml", optional: true, reloadOnChange: false);
					}
				}
			)
			.ConfigureServices(
				(context, services) => {
					AppSettings appSettings = context.Configuration.GetSection(nameof(AppSettings)).Get<AppSettings>() ?? new AppSettings();
					services.AddSingleton(appSettings);
					services.AddHostedService<ProxyService>();
					services.AddSingleton<INetworkService, NetworkService>();
					services.AddSingleton<IWolPacketSender, DebouncedWolPacketSender>();
					services.AddLogging(configure => configure.AddConsole());
				}
			);

}