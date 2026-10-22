namespace YamBassPlayer.Services;

/// <summary>
/// Файловое состояние плагина: один плоский JSON-объект на плагин в каталоге
/// <c>plugin_state/&lt;id&gt;.json</c>. Ядро даёт только неймспейс и атомарную запись,
/// структуру состояния определяет плагин.
/// </summary>
public interface IPluginStateFile
{
	/// <summary>Каталог, в котором лежат файлы состояния плагинов.</summary>
	string Directory { get; }

	/// <summary>Возвращает путь к файлу состояния указанного плагина.</summary>
	string GetPath(string pluginId);

	/// <summary>
	/// Читает значение ключа. Отсутствующий или повреждённый файл, а также
	/// отсутствующий ключ дают <c>default</c> и запись предупреждения в лог.
	/// </summary>
	T? Get<T>(string pluginId, string key);

	/// <summary>
	/// Записывает значение ключа, сохраняя остальные ключи файла. Запись атомарная:
	/// временный файл рядом с целевым, затем переименование.
	/// </summary>
	void Set<T>(string pluginId, string key, T value);
}
