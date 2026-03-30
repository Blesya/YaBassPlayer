using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace YamBassPlayer.Configuration;

public class AppConfiguration
{
	private static IConfiguration? _configuration;
	private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

	// In-memory cache for read-heavy values; invalidated on every write.
	private static JsonObject? _cachedRoot;

	public static IConfiguration Configuration
	{
		get
		{
			if (_configuration == null)
				ReloadConfiguration();
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
		try { return Configuration["YandexMusic:Token"]; }
		catch { return null; }
	}

	public static string YandexMusicToken => GetYandexMusicToken()
		?? throw new InvalidOperationException("YandexMusic:Token not found in configuration");

	public static void SaveToken(string token)
	{
		var root = LoadJsonNode();
		var section = root["YandexMusic"]?.AsObject() ?? new JsonObject();
		section["Token"] = token;
		root["YandexMusic"] = section;
		SaveJsonNode(root);
	}

	public static float[] GetEqualizerBands()
	{
		try
		{
			var bands = LoadJsonNode()["Equalizer"]?["Bands"]?.AsArray();
			if (bands != null && bands.Count == 10)
			{
				var values = new float[10];
				for (int i = 0; i < 10; i++)
					values[i] = bands[i]?.GetValue<float>() ?? 0f;
				return values;
			}
		}
		catch { }
		return new float[10];
	}

	public static void SaveEqualizerBands(float[] bands)
	{
		if (bands.Length != 10)
			throw new ArgumentException("Equalizer must have exactly 10 bands");

		var root = LoadJsonNode();
		var bandsArray = new JsonArray();
		foreach (var band in bands)
			bandsArray.Add(band);

		var section = root["Equalizer"]?.AsObject() ?? new JsonObject();
		section["Bands"] = bandsArray;
		root["Equalizer"] = section;
		SaveJsonNode(root);
	}

	public static string? GetTheme()
	{
		try { return LoadJsonNode()["Theme"]?.GetValue<string>(); }
		catch { return null; }
	}

	public static void SaveTheme(string themeName)
	{
		var root = LoadJsonNode();
		root["Theme"] = themeName;
		SaveJsonNode(root);
	}

	public static int GetSessionGapMinutes()
	{
		try { return LoadJsonNode()["Recommendation"]?["SessionGapMinutes"]?.GetValue<int>() ?? 20; }
		catch { return 20; }
	}

	public static void SaveSessionGapMinutes(int minutes)
	{
		var root = LoadJsonNode();
		var section = root["Recommendation"]?.AsObject() ?? new JsonObject();
		section["SessionGapMinutes"] = minutes;
		root["Recommendation"] = section;
		SaveJsonNode(root);
	}

	/// <summary>
	/// Returns the list of local music folders from configuration.
	/// Reads <c>LocalMusic:Folders</c> as a JSON string array, or a single
	/// semicolon-separated string value. Returns an empty array when not configured.
	/// </summary>
	public static string[] GetLocalMusicFolders()
	{
		try
		{
			// Try binding as a proper JSON array (["C:\\Music", "D:\\Music"])
			var section = Configuration.GetSection("LocalMusic:Folders");
			if (section.Exists())
			{
				var values = section.Get<string[]>();
				if (values is { Length: > 0 })
					return values;
			}

			// Fall back to a semicolon-separated scalar: "C:\\Music;D:\\Music"
			var raw = Configuration["LocalMusic:Folders"];
			if (!string.IsNullOrWhiteSpace(raw))
				return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		}
		catch { }

		return [];
	}

	private static JsonObject LoadJsonNode()
	{
		if (_cachedRoot != null)
			return _cachedRoot;

		if (File.Exists(ConfigFilePath))
		{
			string existingJson = File.ReadAllText(ConfigFilePath);
			_cachedRoot = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
		}
		else
		{
			_cachedRoot = new JsonObject();
		}

		return _cachedRoot;
	}

	private static void SaveJsonNode(JsonObject root)
	{
		var options = new JsonSerializerOptions { WriteIndented = true };
		File.WriteAllText(ConfigFilePath, root.ToJsonString(options));

		_cachedRoot = null;
		_configuration = null;
		ReloadConfiguration();
	}
}
