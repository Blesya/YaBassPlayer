using YamBassPlayer.Updater;

namespace YamBassPlayer.Tests.Updater;

[TestFixture]
public sealed class UpdateApplierTests
{
    private string _directory = null!;
    private string _staging = null!;
    private string _target = null!;
    private string _backup = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ybp-applier-tests", Guid.NewGuid().ToString("N"));
        _staging = Path.Combine(_directory, "staging");
        _target = Path.Combine(_directory, "install");
        _backup = Path.Combine(_directory, "backup");

        Directory.CreateDirectory(_staging);
        Directory.CreateDirectory(_target);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void Apply_ReplacesPayloadAndKeepsUserData()
    {
        Write(_staging, "YamBassPlayer.exe", "new-app");
        Write(_staging, "bass.dll", "new-bass");

        Write(_target, "YamBassPlayer.exe", "old-app");
        Write(_target, "bass.dll", "old-bass");
        Write(_target, "appsettings.json", "{\"token\":\"secret\"}");
        Write(_target, "tracks_cache.db", "cache");
        Directory.CreateDirectory(Path.Combine(_target, "tracks"));
        Write(Path.Combine(_target, "tracks"), "song.mp3", "audio");

        UpdateApplyResult result = UpdateApplier.Apply(
            Payload("YamBassPlayer.exe", "bass.dll"), _target, _backup);

        Assert.That(result.Replaced, Is.EqualTo(2));
        Assert.That(result.Added, Is.EqualTo(0));
        Assert.That(Read(_target, "YamBassPlayer.exe"), Is.EqualTo("new-app"));
        Assert.That(Read(_target, "bass.dll"), Is.EqualTo("new-bass"));
        Assert.That(Read(_target, "appsettings.json"), Is.EqualTo("{\"token\":\"secret\"}"));
        Assert.That(Read(_target, "tracks_cache.db"), Is.EqualTo("cache"));
        Assert.That(Read(Path.Combine(_target, "tracks"), "song.mp3"), Is.EqualTo("audio"));
        Assert.That(Read(_backup, "YamBassPlayer.exe"), Is.EqualTo("old-app"));
    }

    [Test]
    public void Apply_NewFile_IsAdded()
    {
        Write(_staging, "bass_fx.dll", "fx");

        UpdateApplyResult result = UpdateApplier.Apply(Payload("bass_fx.dll"), _target, _backup);

        Assert.That(result.Added, Is.EqualTo(1));
        Assert.That(Read(_target, "bass_fx.dll"), Is.EqualTo("fx"));
    }

    [Test]
    public void Apply_CopyFails_RollsBackEverything()
    {
        Write(_staging, "YamBassPlayer.exe", "new-app");
        Write(_staging, "bass.dll", "new-bass");

        Write(_target, "YamBassPlayer.exe", "old-app");
        // Каталог вместо файла — копирование второго файла обязано упасть.
        Directory.CreateDirectory(Path.Combine(_target, "bass.dll"));

        Assert.Throws(Is.InstanceOf<Exception>(), () =>
            UpdateApplier.Apply(Payload("YamBassPlayer.exe", "bass.dll"), _target, _backup));

        Assert.That(Read(_target, "YamBassPlayer.exe"), Is.EqualTo("old-app"));
        Assert.That(Directory.Exists(Path.Combine(_target, "bass.dll")), Is.True);
    }

    [Test]
    public void Apply_PluginFiles_KeepRelativeLayout()
    {
        string pluginDirectory = Path.Combine(_staging, "plugins", "yandex-music");
        Directory.CreateDirectory(pluginDirectory);
        Write(pluginDirectory, "plugin.dll", "new-plugin");

        Directory.CreateDirectory(Path.Combine(_target, "plugins", "yandex-music"));
        Write(Path.Combine(_target, "plugins", "yandex-music"), "plugin.dll", "old-plugin");

        UpdateApplyResult result = UpdateApplier.Apply(
            Payload(Path.Combine("plugins", "yandex-music", "plugin.dll")),
            _target,
            _backup,
            _staging);

        Assert.That(result.Replaced, Is.EqualTo(1));
        Assert.That(Read(Path.Combine(_target, "plugins", "yandex-music"), "plugin.dll"), Is.EqualTo("new-plugin"));
        Assert.That(Read(Path.Combine(_backup, "plugins", "yandex-music"), "plugin.dll"), Is.EqualTo("old-plugin"));
    }

    [Test]
    public void Apply_PluginUpdate_RemovesObsoleteFiles()
    {
        string stagingPlugin = Path.Combine(_staging, "plugins", "yandex-music");
        Directory.CreateDirectory(stagingPlugin);
        Write(stagingPlugin, "new.dll", "new");

        string targetPlugin = Path.Combine(_target, "plugins", "yandex-music");
        Directory.CreateDirectory(targetPlugin);
        Write(targetPlugin, "old.dll", "old");
        Write(targetPlugin, "plugin.json", "{}");

        UpdateApplyResult result = UpdateApplier.Apply(
            Payload(Path.Combine("plugins", "yandex-music", "new.dll")),
            _target,
            _backup,
            _staging);

        Assert.Multiple(() =>
        {
            Assert.That(result.Added, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(targetPlugin, "old.dll")), Is.False);
            Assert.That(File.Exists(Path.Combine(targetPlugin, "plugin.json")), Is.False);
            Assert.That(Read(targetPlugin, "new.dll"), Is.EqualTo("new"));
            Assert.That(
                Read(Path.Combine(_backup, "plugins", "yandex-music"), "old.dll"),
                Is.EqualTo("old"));
        });
    }

    private List<string> Payload(params string[] names)
        => names.Select(name => Path.Combine(_staging, name)).ToList();

    private static void Write(string directory, string name, string content)
        => File.WriteAllText(Path.Combine(directory, name), content);

    private static string Read(string directory, string name)
        => File.ReadAllText(Path.Combine(directory, name));
}
