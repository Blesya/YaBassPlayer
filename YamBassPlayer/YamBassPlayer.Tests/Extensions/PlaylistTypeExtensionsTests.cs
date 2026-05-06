using YamBassPlayer.Enums;

namespace YamBassPlayer.Tests.Extensions;

[TestFixture]
public sealed class PlaylistTypeExtensionsTests
{
    [TestCase(PlaylistType.Favorite, PlaylistCategory.Data)]
    [TestCase(PlaylistType.Custom, PlaylistCategory.Data)]
    [TestCase(PlaylistType.PlaylistOfTheDaily, PlaylistCategory.Data)]
    [TestCase(PlaylistType.LocalFolder, PlaylistCategory.Data)]
    [TestCase(PlaylistType.LocalFavorite, PlaylistCategory.Data)]
    [TestCase(PlaylistType.Top10, PlaylistCategory.Computed)]
    [TestCase(PlaylistType.TopEvenings, PlaylistCategory.Computed)]
    [TestCase(PlaylistType.TopByDay, PlaylistCategory.Computed)]
    [TestCase(PlaylistType.Cached, PlaylistCategory.Computed)]
    [TestCase(PlaylistType.Queue, PlaylistCategory.Transient)]
    [TestCase(PlaylistType.MyWave, PlaylistCategory.Transient)]
    [TestCase(PlaylistType.LocalSearch, PlaylistCategory.Transient)]
    [TestCase(PlaylistType.YandexSearch, PlaylistCategory.Transient)]
    [TestCase(PlaylistType.Artist, PlaylistCategory.Entity)]
    [TestCase(PlaylistType.LocalArtist, PlaylistCategory.Entity)]
    [TestCase(PlaylistType.LocalAlbum, PlaylistCategory.Entity)]
    public void GetCategory_ReturnsExpectedCategory(PlaylistType type, PlaylistCategory expected)
    {
        var result = type.GetCategory();
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void GetCategory_UndefinedValue_ReturnsData()
    {
        var undefined = (PlaylistType)99;
        var result = undefined.GetCategory();
        Assert.That(result, Is.EqualTo(PlaylistCategory.Data));
    }
}
