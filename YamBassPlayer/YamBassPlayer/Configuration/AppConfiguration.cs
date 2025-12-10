using System.Text.Json;
using System.Text.Json.Nodes;
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
		var root = LoadJsonNode();
		root["YandexMusic"] = new JsonObject { ["Token"] = token };
		SaveJsonNode(root);
	}

	public static float[] GetEqualizerBands()
	{
		try
		{
			var root = LoadJsonNode();
			var bands = root["Equalizer"]?["Bands"]?.AsArray();
			if (bands != null && bands.Count == 10)
			{
				var values = new float[10];
				for (int i = 0; i < 10; i++)
				{
					values[i] = bands[i]?.GetValue<float>() ?? 0f;
				}
				return values;
			}
		}
		catch
		{
			// ignore
		}
		return new float[10];
	}

	public static void SaveEqualizerBands(float[] bands)
	{
		if (bands.Length != 10)
			throw new ArgumentException("Equalizer must have exactly 10 bands");

		var root = LoadJsonNode();
		var bandsArray = new JsonArray();
		foreach (var band in bands)
		{
			bandsArray.Add(band);
		}
		root["Equalizer"] = new JsonObject { ["Bands"] = bandsArray };
		SaveJsonNode(root);
	}

	public static string? GetTheme()
	{
		try
		{
			var root = LoadJsonNode();
			return root["Theme"]?.GetValue<string>();
		}
		catch
		{
			return null;
		}
	}

	public static void SaveTheme(string themeName)
	{
		var root = LoadJsonNode();
		root["Theme"] = themeName;
		SaveJsonNode(root);
	}

	private static JsonObject LoadJsonNode()
	{
		if (File.Exists(ConfigFilePath))
		{
			string existingJson = File.ReadAllText(ConfigFilePath);
			return JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
		}
		return new JsonObject();
	}

	private static void SaveJsonNode(JsonObject root)
	{
		var options = new JsonSerializerOptions { WriteIndented = true };
		string json = root.ToJsonString(options);
		File.WriteAllText(ConfigFilePath, json);

		_configuration = null;
		ReloadConfiguration();
	}
}