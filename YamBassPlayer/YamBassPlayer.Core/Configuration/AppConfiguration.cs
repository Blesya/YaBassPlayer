using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using YamBassPlayer.Models;

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

	/// <summary>
	/// Режим TrueColor: "auto" (по умолчанию — если терминал умеет VT),
	/// "on" — пробовать всегда, "off" — только 16 цветов.
	/// </summary>
	public static string GetTrueColorMode()
	{
		try { return LoadJsonNode()["Ui"]?["TrueColor"]?.GetValue<string>() ?? "auto"; }
		catch { return "auto"; }
	}

	public static void SaveTheme(string themeName)
	{
		var root = LoadJsonNode();
		root["Theme"] = themeName;
		SaveJsonNode(root);
	}

	/// <summary>Возвращает сохранённый режим отображения треков (true — плитки, false — таблица).</summary>
	public static bool GetTracksViewMode()
	{
		try { return LoadJsonNode()["View"]?["TracksMode"]?.GetValue<string>() != "table"; }
		catch { return true; }
	}

	public static void SaveTracksViewMode(bool useTiles)
	{
		var root = LoadJsonNode();
		var section = root["View"]?.AsObject() ?? new JsonObject();
		section["TracksMode"] = useTiles ? "tiles" : "table";
		root["View"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Видна ли панель «Инфо» справа от списка треков. true — не сохранялась (по умолчанию).</summary>
	public static bool GetInfoPanelVisible()
	{
		try { return LoadJsonNode()["View"]?["InfoPanelVisible"]?.GetValue<bool>() ?? true; }
		catch { return true; }
	}

	public static void SaveInfoPanelVisible(bool visible)
	{
		var root = LoadJsonNode();
		var section = root["View"]?.AsObject() ?? new JsonObject();
		section["InfoPanelVisible"] = visible;
		root["View"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Свёрнуто ли дерево плейлистов. false — не сохранялось (по умолчанию развёрнуто).</summary>
	public static bool GetPlaylistsCollapsed()
	{
		try { return LoadJsonNode()["View"]?["PlaylistsCollapsed"]?.GetValue<bool>() ?? false; }
		catch { return false; }
	}

	public static void SavePlaylistsCollapsed(bool collapsed)
	{
		var root = LoadJsonNode();
		var section = root["View"]?.AsObject() ?? new JsonObject();
		section["PlaylistsCollapsed"] = collapsed;
		root["View"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Возвращает сохранённый индекс визуализации (режима спектра). -1 — не сохранялся (по умолчанию).</summary>
	public static int GetSpectrumMode()
	{
		try { return LoadJsonNode()["Spectrum"]?["Mode"]?.GetValue<int>() ?? -1; }
		catch { return -1; }
	}

	public static void SaveSpectrumMode(int modeIndex)
	{
		var root = LoadJsonNode();
		var section = root["Spectrum"]?.AsObject() ?? new JsonObject();
		section["Mode"] = modeIndex;
		root["Spectrum"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Возвращает сохранённую частоту визуализации (Гц). 0 — не сохранялась (по умолчанию 22050).</summary>
	public static int GetSpectrumFrequency()
	{
		try { return LoadJsonNode()["Spectrum"]?["Frequency"]?.GetValue<int>() ?? 0; }
		catch { return 0; }
	}

	public static void SaveSpectrumFrequency(int frequencyHz)
	{
		var root = LoadJsonNode();
		var section = root["Spectrum"]?.AsObject() ?? new JsonObject();
		section["Frequency"] = frequencyHz;
		root["Spectrum"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Включена ли визуализация спектра. true — не сохранялась (по умолчанию).</summary>
	public static bool GetSpectrumEnabled()
	{
		try { return LoadJsonNode()["Spectrum"]?["Enabled"]?.GetValue<bool>() ?? true; }
		catch { return true; }
	}

	public static void SaveSpectrumEnabled(bool enabled)
	{
		var root = LoadJsonNode();
		var section = root["Spectrum"]?.AsObject() ?? new JsonObject();
		section["Enabled"] = enabled;
		root["Spectrum"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Возвращает сохранённую громкость в процентах. 100 — не сохранялась.</summary>
	public static int GetVolumePercent()
	{
		try { return LoadJsonNode()["Playback"]?["Volume"]?.GetValue<int>() ?? 100; }
		catch { return 100; }
	}

	public static void SaveVolumePercent(int percent)
	{
		var root = LoadJsonNode();
		var section = root["Playback"]?.AsObject() ?? new JsonObject();
		section["Volume"] = percent;
		root["Playback"] = section;
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

	/// <summary>
	/// Переносит старые ключи <c>YandexMusic:*</c> в <c>Plugins:yandex-music</c>.
	/// Вызывается один раз при старте: после переезда в плагин токены должны остаться.
	/// </summary>
	public static void MigrateLegacyYandexSettings()
	{
		try
		{
			var root = LoadJsonNode();
			if (root["YandexMusic"] is not JsonObject legacy)
				return;

			var plugins = root["Plugins"]?.AsObject() ?? new JsonObject();
			var section = plugins["yandex-music"]?.AsObject() ?? new JsonObject();
			bool changed = false;

			changed |= Copy("Token", "token");
			changed |= Copy("RefreshToken", "refreshToken");
			changed |= Copy("DeviceId", "deviceId");

			if (!changed)
				return;

			plugins["yandex-music"] = section;
			root["Plugins"] = plugins;
			SaveJsonNode(root);

			bool Copy(string legacyKey, string pluginKey)
			{
				if (section[pluginKey] is not null)
					return false;

				string? value = legacy[legacyKey]?.GetValue<string>();
				if (string.IsNullOrWhiteSpace(value))
					return false;

				section[pluginKey] = value;
				return true;
			}
		}
		catch
		{
			// Миграция не должна мешать запуску: если конфиг битый, плагин просто попросит вход.
		}
	}

	/// <summary>Значение настройки плагина из секции <c>Plugins:&lt;id&gt;</c>.</summary>
	public static string? GetPluginSetting(string pluginId, string key)
	{
		try
		{
			return LoadJsonNode()["Plugins"]?[pluginId]?[key]?.GetValue<string>();
		}
		catch { return null; }
	}

	/// <summary>Сохраняет настройку плагина в секцию <c>Plugins:&lt;id&gt;</c>.</summary>
	public static void SavePluginSetting(string pluginId, string key, string? value)
	{
		var root = LoadJsonNode();
		var plugins = root["Plugins"]?.AsObject() ?? new JsonObject();
		var section = plugins[pluginId]?.AsObject() ?? new JsonObject();
		section[key] = value;
		plugins[pluginId] = section;
		root["Plugins"] = plugins;
		SaveJsonNode(root);
	}

	/// <summary>Включена ли автоматическая проверка обновлений. true — не сохранялась (по умолчанию).</summary>
	public static bool GetUpdatesEnabled()
	{
		try { return LoadJsonNode()["Updates"]?["Enabled"]?.GetValue<bool>() ?? true; }
		catch { return true; }
	}

	public static void SaveUpdatesEnabled(bool enabled)
	{
		var root = LoadJsonNode();
		var section = root["Updates"]?.AsObject() ?? new JsonObject();
		section["Enabled"] = enabled;
		root["Updates"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Сохранённый размер терминального окна. <c>null</c> — ещё не сохранялся.</summary>
	public static ConsoleWindowSize? GetWindowSize()
	{
		try
		{
			var window = LoadJsonNode()["Window"];
			int? width = window?["Width"]?.GetValue<int>();
			int? height = window?["Height"]?.GetValue<int>();
			if (width is null || height is null)
				return null;

			return new ConsoleWindowSize(width.Value, height.Value);
		}
		catch { return null; }
	}

	public static void SaveWindowSize(int width, int height)
	{
		var root = LoadJsonNode();
		var section = root["Window"]?.AsObject() ?? new JsonObject();
		section["Width"] = width;
		section["Height"] = height;
		root["Window"] = section;
		SaveJsonNode(root);
	}

	/// <summary>Сохранённое положение терминального окна. <c>null</c> — ещё не сохранялось.</summary>
	public static ConsoleWindowPosition? GetWindowPosition()
	{
		try
		{
			var window = LoadJsonNode()["Window"];
			int? x = window?["Left"]?.GetValue<int>();
			int? y = window?["Top"]?.GetValue<int>();
			if (x is null || y is null)
				return null;

			return new ConsoleWindowPosition(x.Value, y.Value);
		}
		catch { return null; }
	}

	public static void SaveWindowPosition(int x, int y)
	{
		var root = LoadJsonNode();
		var section = root["Window"]?.AsObject() ?? new JsonObject();
		section["Left"] = x;
		section["Top"] = y;
		root["Window"] = section;
		SaveJsonNode(root);
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
