namespace YamBassPlayer.Plugins;

/// <summary>Лог плагина: хост подставляет префикс <c>[plugin:&lt;id&gt;]</c>.</summary>
public interface IPluginLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message, Exception? exception = null);
    void Error(string message, Exception? exception = null);
}

/// <summary>Состояние плагина: отдельный JSON-файл на плагин.</summary>
public interface IPluginStateStore
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
}

/// <summary>Конфигурация плагина: секция <c>Plugins:&lt;id&gt;</c>.</summary>
public interface IPluginSettings
{
    string? Get(string key);
    void Set(string key, string? value);
}
