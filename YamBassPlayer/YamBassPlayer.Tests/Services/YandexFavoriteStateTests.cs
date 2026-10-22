using System.Text.Json;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class YandexFavoriteStateTests
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
    public void Initialize_PersistsFavoriteIds()
    {
        var service = CreateService();

        service.Initialize(["987654", "987655"]);

        var path = Path.Combine(_directory, "yandex-music.json");
        Assert.That(File.Exists(path), Is.True);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var ids = document.RootElement.GetProperty("favoriteTrackIds")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.That(ids, Is.EquivalentTo(new[] { "987654", "987655" }));
    }

    [Test]
    public void ConstructedService_ReadsPersistedIds()
    {
        CreateService().Initialize(["111", "222"]);

        var restored = CreateService();

        Assert.Multiple(() =>
        {
            Assert.That(restored.IsTrackFavorite("111"), Is.True);
            Assert.That(restored.IsTrackFavorite("222"), Is.True);
            Assert.That(restored.IsTrackFavorite("333"), Is.False);
        });
    }

    private YandexFavoriteService CreateService() =>
        new(new YandexMusicApi(), new AuthStorage(), new PluginStateStore(new PluginStateFile(_directory), "yandex-music"));
}
