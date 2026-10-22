using System.Reflection;
using System.Runtime.Loader;
using Serilog;

namespace YamBassPlayer.Plugins;

public enum PluginLoadStatus
{
    Loaded,
    Failed,
    Incompatible
}

/// <summary>Результат загрузки одного плагина для меню и диагностики.</summary>
public sealed record PluginLoadReport(
    string PluginId,
    string Name,
    string Path,
    PluginLoadStatus Status,
    string? Error = null,
    string? Version = null,
    string? ContractVersion = null)
{
    public bool IsLoaded => Status == PluginLoadStatus.Loaded;
}

/// <summary>
/// Находит плагины в <c>plugins/*/plugin.json</c>, проверяет версию контракта,
/// загружает сборку в отдельный контекст и вызывает <see cref="IPlugin.Register"/>.
/// Ошибка одного плагина не мешает остальным.
/// </summary>
public sealed class PluginLoader(
    IEnumerable<IPlugin> builtInPlugins,
    PluginLoadReportStore reportStore,
    Func<IPlugin, IPluginRegistrar> registrarFactory)
{
    private readonly List<IPlugin> _loadedPlugins = [];

    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins;

    public IReadOnlyList<PluginLoadReport> LoadAll(string pluginsDirectory)
    {
        var reports = new List<PluginLoadReport>();

        foreach (IPlugin plugin in builtInPlugins)
            reports.Add(RegisterPlugin(plugin, "встроенный"));

        if (Directory.Exists(pluginsDirectory))
        {
            foreach (string pluginDirectory in Directory.GetDirectories(pluginsDirectory).OrderBy(path => path, StringComparer.Ordinal))
            {
                string manifestPath = Path.Combine(pluginDirectory, "plugin.json");
                if (!File.Exists(manifestPath))
                    continue;

                reports.Add(LoadPlugin(pluginDirectory, manifestPath));
            }
        }

        reportStore.Set(reports);
        return reports;
    }

    private PluginLoadReport RegisterPlugin(IPlugin plugin, string path)
    {
        try
        {
            plugin.Register(registrarFactory(plugin));
            _loadedPlugins.Add(plugin);
            return new PluginLoadReport(plugin.Id, plugin.DisplayName, path, PluginLoadStatus.Loaded);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось зарегистрировать плагин {PluginId}", plugin.Id);
            return new PluginLoadReport(plugin.Id, plugin.DisplayName, path, PluginLoadStatus.Failed, ex.Message);
        }
    }

    private PluginLoadReport LoadPlugin(string pluginDirectory, string manifestPath)
    {
        string directoryName = Path.GetFileName(pluginDirectory);

        if (!PluginManifest.TryLoad(manifestPath, out PluginManifest? manifest, out string? manifestError) || manifest is null)
            return new PluginLoadReport(directoryName, directoryName, pluginDirectory, PluginLoadStatus.Failed, manifestError);

        if (!string.Equals(manifest.Id, directoryName, StringComparison.Ordinal))
        {
            return new PluginLoadReport(
                manifest.Id,
                manifest.Name,
                pluginDirectory,
                PluginLoadStatus.Failed,
                $"id «{manifest.Id}» не совпадает с именем папки «{directoryName}»",
                manifest.Version,
                manifest.ContractVersion);
        }

        if (!PluginContract.IsCompatible(manifest.ContractVersion))
        {
            return new PluginLoadReport(
                manifest.Id,
                manifest.Name,
                pluginDirectory,
                PluginLoadStatus.Incompatible,
                $"требует контракт {manifest.ContractVersion}, у приложения {PluginContract.Major}.{PluginContract.Minor}",
                manifest.Version,
                manifest.ContractVersion);
        }

        try
        {
            string entryPath = ResolveEntryAssembly(pluginDirectory, manifest);
            var context = new PluginLoadContext(entryPath);
            Assembly assembly = context.LoadFromAssemblyPath(entryPath);

            List<Type> pluginTypes = assembly.GetTypes()
                .Where(type => typeof(IPlugin).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsInterface: false })
                .ToList();

            if (pluginTypes.Count == 0)
                return Failed(manifest, pluginDirectory, "не найден тип IPlugin");

            if (pluginTypes.Count > 1)
            {
                return Failed(
                    manifest,
                    pluginDirectory,
                    "неоднозначно: " + string.Join(", ", pluginTypes.Select(type => type.FullName)));
            }

            if (Activator.CreateInstance(pluginTypes[0]) is not IPlugin plugin)
                return Failed(manifest, pluginDirectory, "не удалось создать экземпляр плагина");

            if (!string.Equals(plugin.Id, manifest.Id, StringComparison.Ordinal))
                return Failed(manifest, pluginDirectory, $"IPlugin.Id «{plugin.Id}» не совпадает с манифестом");

            plugin.Register(registrarFactory(plugin));
            _loadedPlugins.Add(plugin);

            Log.Information("Плагин загружен: {PluginId} {Version}", manifest.Id, manifest.Version);
            return new PluginLoadReport(manifest.Id, manifest.Name, pluginDirectory, PluginLoadStatus.Loaded,
                Version: manifest.Version, ContractVersion: manifest.ContractVersion);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось загрузить плагин {PluginId}", manifest.Id);
            return Failed(manifest, pluginDirectory, ex.Message);
        }
    }

    private static string ResolveEntryAssembly(string pluginDirectory, PluginManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            string explicitPath = Path.Combine(pluginDirectory, manifest.EntryAssembly);
            if (!File.Exists(explicitPath))
                throw new FileNotFoundException($"не найдена сборка «{manifest.EntryAssembly}»", explicitPath);

            return explicitPath;
        }

        string[] candidates = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        if (candidates.Length == 1)
            return candidates[0];

        throw new InvalidOperationException(
            "не указан entryAssembly, а DLL в папке несколько: " + string.Join(", ", candidates.Select(Path.GetFileName)));
    }

    private static PluginLoadReport Failed(PluginManifest manifest, string directory, string error)
        => new(manifest.Id, manifest.Name, directory, PluginLoadStatus.Failed, error,
            manifest.Version, manifest.ContractVersion);
}
