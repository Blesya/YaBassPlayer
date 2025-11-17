using Microsoft.Extensions.Configuration;

namespace YamBassPlayer.Configuration
{
	public class AppConfiguration
	{
		private static IConfiguration? _configuration;

		public static IConfiguration Configuration
		{
			get
			{
				if (_configuration == null)
				{
					_configuration = new ConfigurationBuilder()
						.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
						.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
						.Build();
				}
				return _configuration;
			}
		}

		public static string YandexMusicToken => Configuration["YandexMusic:Token"] 
			?? throw new InvalidOperationException("YandexMusic:Token not found in configuration");
	}
}
