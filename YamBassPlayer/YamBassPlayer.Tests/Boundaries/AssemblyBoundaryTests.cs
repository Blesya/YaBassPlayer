namespace YamBassPlayer.Tests.Boundaries;

using System.Reflection;

internal static class AssemblyReferences
{
    public static string[] ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
}

// Каждая проверка грузит только ту сборку, о которой она, и падает только на ней.
// Общая загрузка в [OneTimeSetUp] уронила бы все тесты сразу, как только одной из
// сборок не окажется на месте, и подтолкнула бы ослабить сами проверки.
[TestFixture]
public sealed class CoreAssemblyBoundaryTests
{
    private const string CoreAssemblyName = "YamBassPlayer.Core";
    private const string YandexApiAssemblyName = "YamBassPlayer.Plugins.YandexMusic.Api";
    private const string PluginAssemblyPrefix = "YamBassPlayer.Plugins.";
    private const string PluginsAssemblyName = "YamBassPlayer.Plugins";
    private const string ContractsAssemblyName = "YamBassPlayer.Contracts";
    private const string ContractsUiAssemblyName = "YamBassPlayer.Contracts.Ui";

    private static string[] LoadCoreReferences() =>
        AssemblyReferences.ReferencedNames(Assembly.Load(new AssemblyName(CoreAssemblyName)));

    [Test]
    public void Core_DoesNotReferenceYandexApiAssembly()
    {
        var references = LoadCoreReferences();

        Assert.That(references, Does.Not.Contain(YandexApiAssemblyName));
    }

    [Test]
    public void Core_DoesNotReferenceAnyPluginAssembly()
    {
        var references = LoadCoreReferences();

        var pluginReferences = references
            .Where(name => name == PluginsAssemblyName
                || name.StartsWith(PluginAssemblyPrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.That(pluginReferences, Is.Empty);
    }

    [Test]
    public void Core_ReferencesContractsAssemblies()
    {
        var references = LoadCoreReferences();

        Assert.That(references, Does.Contain(ContractsAssemblyName));
        Assert.That(references, Does.Contain(ContractsUiAssemblyName));
    }
}

// Контракты не должны знать о терминальном UI, поэтому Terminal.Gui живёт только
// в отдельной сборке UI-контрактов. Оба теста вместе фиксируют это разделение.
[TestFixture]
public sealed class ContractsAssemblyBoundaryTests
{
    private const string ContractsAssemblyName = "YamBassPlayer.Contracts";
    private const string ContractsUiAssemblyName = "YamBassPlayer.Contracts.Ui";
    private const string TerminalGuiAssemblyName = "Terminal.Gui";

    [Test]
    public void Contracts_DoesNotReferenceTerminalGui()
    {
        var references = AssemblyReferences.ReferencedNames(
            Assembly.Load(new AssemblyName(ContractsAssemblyName)));

        Assert.That(references, Is.Not.Empty);
        Assert.That(references, Does.Not.Contain(TerminalGuiAssemblyName));
    }

    [Test]
    public void ContractsUi_ReferencesTerminalGui()
    {
        var references = AssemblyReferences.ReferencedNames(
            Assembly.Load(new AssemblyName(ContractsUiAssemblyName)));

        Assert.That(references, Does.Contain(TerminalGuiAssemblyName));
    }
}
