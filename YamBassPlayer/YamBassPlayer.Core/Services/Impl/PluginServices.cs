using Serilog;
using YamBassPlayer.Configuration;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

/// <summary>Состояние плагина поверх общего файла состояния.</summary>
public sealed class PluginStateStore(IPluginStateFile stateFile, string pluginId) : IPluginStateStore
{
    public T? Get<T>(string key) => stateFile.Get<T>(pluginId, key);

    public void Set<T>(string key, T value) => stateFile.Set(pluginId, key, value);
}

/// <summary>Настройки плагина: секция <c>Plugins:&lt;id&gt;</c> в appsettings.json.</summary>
public sealed class PluginSettings(string pluginId) : IPluginSettings
{
    public string? Get(string key) => AppConfiguration.GetPluginSetting(pluginId, key);

    public void Set(string key, string? value) => AppConfiguration.SavePluginSetting(pluginId, key, value);
}

/// <summary>Лог плагина с префиксом <c>[plugin:&lt;id&gt;]</c>.</summary>
public sealed class PluginLogger(string pluginId) : IPluginLogger
{
    private string Prefix => $"[plugin:{pluginId}] ";

    public void Debug(string message) => Log.Debug("{Message}", Prefix + message);

    public void Info(string message) => Log.Information("{Message}", Prefix + message);

    public void Warn(string message, Exception? exception = null) => Log.Warning(exception, "{Message}", Prefix + message);

    public void Error(string message, Exception? exception = null) => Log.Error(exception, "{Message}", Prefix + message);
}
