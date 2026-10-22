using System.Reflection;
using System.Runtime.Loader;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Контекст загрузки одного плагина. Общие сборки (контракты и Terminal.Gui)
/// резолвятся контекстом по умолчанию — иначе типы плагина и хоста не совпадут.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "YamBassPlayer.Contracts",
        "YamBassPlayer.Contracts.Ui",
        "Terminal.Gui",
        // Terminal.Gui построен на NStack.ustring: если плагин загрузит свою копию,
        // типы ustring не совпадут и конструкторы Terminal.Gui «исчезнут».
        "NStack",
        "NStack.Core",
        "Serilog"
    };

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string entryAssemblyPath)
        : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
            return null;

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
