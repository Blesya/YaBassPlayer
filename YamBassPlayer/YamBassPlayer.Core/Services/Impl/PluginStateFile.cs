using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Serilog;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Плоский JSON-объект на плагин в <c>plugin_state/&lt;id&gt;.json</c>.
/// Запись атомарная (временный файл + переименование), поэтому оборванная запись
/// не оставляет полуфайл. Повреждённый файл читается как пустой и логируется.
/// </summary>
public sealed class PluginStateFile(string directory) : IPluginStateFile
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly object _sync = new();

	public string Directory => directory;

	public string GetPath(string pluginId) => Path.Combine(directory, pluginId + ".json");

	public T? Get<T>(string pluginId, string key)
	{
		var path = GetPath(pluginId);

		lock (_sync)
		{
			if (!File.Exists(path))
			{
				Log.Warning("Файл состояния плагина не найден: {Path}", path);
				return default;
			}

			var state = ReadState(path);
			if (state is null)
				return default;

			if (!state.TryGetPropertyValue(key, out var node) || node is null)
			{
				Log.Warning("В файле состояния плагина {Path} нет ключа {Key}", path, key);
				return default;
			}

			try
			{
				return node.Deserialize<T>(Options);
			}
			catch (JsonException exception)
			{
				Log.Warning(exception, "Не удалось прочитать ключ {Key} из файла состояния плагина {Path}", key, path);
				return default;
			}
		}
	}

	public void Set<T>(string pluginId, string key, T value)
	{
		var path = GetPath(pluginId);

		lock (_sync)
		{
			try
			{
				var state = ReadState(path) ?? new JsonObject();
				state[key] = JsonSerializer.SerializeToNode(value, Options);

				System.IO.Directory.CreateDirectory(directory);

				// Временный файл лежит в том же каталоге, поэтому переименование атомарно.
				var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
				try
				{
					File.WriteAllText(tempPath, state.ToJsonString(Options));
					File.Move(tempPath, path, overwrite: true);
				}
				finally
				{
					if (File.Exists(tempPath))
						File.Delete(tempPath);
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
			{
				// Состояние плагина не критично для воспроизведения: пишем в лог и продолжаем.
				Log.Warning(exception, "Не удалось записать файл состояния плагина {Path}", path);
			}
		}
	}

	private static JsonObject? ReadState(string path)
	{
		try
		{
			var root = JsonNode.Parse(File.ReadAllText(path));
			if (root is JsonObject state)
				return state;

			Log.Warning("Корень файла состояния плагина не является JSON-объектом: {Path}", path);
			return null;
		}
		catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
		{
			Log.Warning(exception, "Файл состояния плагина повреждён и будет прочитан как пустой: {Path}", path);
			return null;
		}
	}
}
