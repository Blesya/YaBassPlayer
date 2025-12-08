using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace YamBassPlayer.Configuration;

public class AppConfiguration
{
	private static IConfiguration? _configuration;
	private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

	public static IConfiguration Configuration
	{
	    get
	    {
	        if (_configuration == null)
	        {
	            ReloadConfiguration();
	        }
	        return _configuration!;
	    }
	}

	private static void ReloadConfiguration()
	{
	    _configuration = new ConfigurationBuilder()
	        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
	        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
	        .Build();
	}

	public static string? GetYandexMusicToken()
	{
	    try
	    {
	        return Configuration["YandexMusic:Token"];
	    }
	    catch
	    {
	        return null;
	    }
	}

	public static string YandexMusicToken => GetYandexMusicToken() 
	                                         ?? throw new InvalidOperationException("YandexMusic:Token not found in configuration");

	public static void SaveToken(string token)
	{
	    Dictionary<string, object> config;

	    if (File.Exists(ConfigFilePath))
	    {
	        string existingJson = File.ReadAllText(ConfigFilePath);
	        config = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) 
	                 ?? new Dictionary<string, object>();
	    }
	    else
	    {
	        config = new Dictionary<string, object>();
	    }

	    config["YandexMusic"] = new Dictionary<string, string> { ["Token"] = token };

	    var options = new JsonSerializerOptions { WriteIndented = true };
	    string json = JsonSerializer.Serialize(config, options);
	    File.WriteAllText(ConfigFilePath, json);

	    _configuration = null;
	    ReloadConfiguration();
	}
}