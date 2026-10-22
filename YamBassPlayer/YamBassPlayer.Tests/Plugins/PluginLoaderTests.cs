using Moq;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Tests.Plugins;

[TestFixture]
public sealed class PluginLoaderTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "ybp-plugins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private sealed class FakePlugin(string id) : IPlugin
    {
        public string Id => id;
        public string DisplayName => id;
        public bool Registered { get; private set; }

        public event Action? Changed;

        public void Register(IPluginRegistrar registrar) => Registered = true;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ConfigureAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private PluginLoader CreateLoader(params IPlugin[] builtIn)
        => new(
            builtIn,
            new PluginLoadReportStore(),
            _ => new Mock<IPluginRegistrar>().Object);

    private void WritePlugin(string id, string contractVersion)
    {
        string directory = Path.Combine(_root, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "plugin.json"),
            $$"""
              { "id": "{{id}}", "name": "{{id}}", "version": "1.0.0", "contractVersion": "{{contractVersion}}" }
              """);
    }

    [Test]
    public void LoadAll_RegistersBuiltInPlugins()
    {
        var plugin = new FakePlugin("local");

        var reports = CreateLoader(plugin).LoadAll(_root);

        Assert.Multiple(() =>
        {
            Assert.That(plugin.Registered, Is.True);
            Assert.That(reports.Single().Status, Is.EqualTo(PluginLoadStatus.Loaded));
        });
    }

    [Test]
    public void LoadAll_ReportsIncompatibleContractVersion()
    {
        WritePlugin("future", "2.0");

        var reports = CreateLoader().LoadAll(_root);

        Assert.That(reports.Single().Status, Is.EqualTo(PluginLoadStatus.Incompatible));
    }

    [Test]
    public void LoadAll_ReportsInvalidManifest()
    {
        string directory = Path.Combine(_root, "broken");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "plugin.json"), "{ not json");

        var reports = CreateLoader().LoadAll(_root);

        Assert.That(reports.Single().Status, Is.EqualTo(PluginLoadStatus.Failed));
    }

    [Test]
    public void LoadAll_ReportsIdMismatchWithFolder()
    {
        string directory = Path.Combine(_root, "folder-name");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "plugin.json"),
            """{ "id": "other", "name": "Other", "version": "1.0.0", "contractVersion": "1.0" }""");

        var reports = CreateLoader().LoadAll(_root);

        Assert.Multiple(() =>
        {
            Assert.That(reports.Single().Status, Is.EqualTo(PluginLoadStatus.Failed));
            Assert.That(reports.Single().Error, Does.Contain("не совпадает"));
        });
    }

    [Test]
    public void LoadAll_EmptyDirectory_ReturnsNoReports()
    {
        Assert.That(CreateLoader().LoadAll(_root), Is.Empty);
    }
}
