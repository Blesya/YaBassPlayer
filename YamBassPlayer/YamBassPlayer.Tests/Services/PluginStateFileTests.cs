using System.Text.Json;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class PluginStateFileTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"plugin_state_{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        if (System.IO.Directory.Exists(_directory))
            System.IO.Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void SetThenGet_RoundTripsValue()
    {
        var state = new PluginStateFile(_directory);

        state.Set("yandex-music", "favoriteTrackIds", new[] { "987654", "987655" });

        Assert.That(
            state.Get<string[]>("yandex-music", "favoriteTrackIds"),
            Is.EqualTo(new[] { "987654", "987655" }));
    }

    [Test]
    public void Get_MissingKeyReturnsDefault()
    {
        var state = new PluginStateFile(_directory);
        state.Set("yandex-music", "favoriteTrackIds", new[] { "1" });

        Assert.That(state.Get<string[]>("yandex-music", "customPlaylistIds"), Is.Null);
    }

    [Test]
    public void Get_CorruptFileReturnsDefaultAndDoesNotThrow()
    {
        var state = new PluginStateFile(_directory);
        System.IO.Directory.CreateDirectory(_directory);
        File.WriteAllText(state.GetPath("yandex-music"), "{ это не json");

        string[]? value = null;
        Assert.DoesNotThrow(() => value = state.Get<string[]>("yandex-music", "favoriteTrackIds"));
        Assert.That(value, Is.Null);
    }

    [Test]
    public void Set_ReplacesFileAtomically()
    {
        var state = new PluginStateFile(_directory);

        state.Set("yandex-music", "favoriteTrackIds", new[] { "1" });
        state.Set("yandex-music", "customPlaylistIds", new Dictionary<string, string[]>
        {
            ["Плейлист дня"] = ["2"]
        });

        var path = state.GetPath("yandex-music");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("favoriteTrackIds")[0].GetString(), Is.EqualTo("1"));
            Assert.That(root.GetProperty("customPlaylistIds").GetProperty("Плейлист дня")[0].GetString(), Is.EqualTo("2"));
            Assert.That(System.IO.Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
            Assert.That(System.IO.Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void GetPath_UsesPluginIdAsFileName()
    {
        var state = new PluginStateFile(_directory);

        Assert.Multiple(() =>
        {
            Assert.That(state.Directory, Is.EqualTo(_directory));
            Assert.That(state.GetPath("yandex-music"), Is.EqualTo(Path.Combine(_directory, "yandex-music.json")));
            Assert.That(state.GetPath("local"), Is.EqualTo(Path.Combine(_directory, "local.json")));
        });
    }
}
