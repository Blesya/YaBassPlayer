using Moq;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

/// <summary>
/// Источник Яндекса реализует радио-возможность ядра и переводит
/// ключи треков в идентификаторы API и обратно.
/// </summary>
[TestFixture]
public sealed class YandexRadioDelegationTests
{
    private static readonly TrackKey Key = new(YandexMusicSourceId.Value, "42");

    [Test]
    public async Task FetchNextBatch_MapsIdsToSourceKeys()
    {
        var radio = new Mock<IYandexRadioService>();
        radio.Setup(r => r.FetchNextBatchAsync())
            .ReturnsAsync((new List<string> { "1", "2" }, new List<Track> { Track("1"), Track("2") }));

        YandexMusicSource source = CreateSource(radio);

        RadioBatch batch = await source.FetchNextBatchAsync();

        Assert.That(batch.Keys.Select(k => k.TrackId), Is.EqualTo(new[] { "1", "2" }));
        Assert.That(batch.Keys.All(k => k.SourceId == YandexMusicSourceId.Value), Is.True);
    }

    [Test]
    public async Task StartTrackRadio_PassesTrackIdToRadioService()
    {
        var radio = new Mock<IYandexRadioService>();
        radio.Setup(r => r.StartTrackWaveAsync("42")).ReturnsAsync(true);

        YandexMusicSource source = CreateSource(radio);

        Assert.That(await source.StartTrackRadioAsync(Key), Is.True);
        radio.Verify(r => r.StartTrackWaveAsync("42"), Times.Once);
    }

    [Test]
    public async Task TrackNotifications_PassTrackIdToRadioService()
    {
        var radio = new Mock<IYandexRadioService>();
        YandexMusicSource source = CreateSource(radio);

        await source.SendTrackStartedAsync(Key);
        await source.SendTrackFinishedAsync(Key, 10);
        await source.SendTrackSkippedAsync(Key, 5);

        Assert.Multiple(() =>
        {
            radio.Verify(r => r.SendTrackStartedAsync("42"), Times.Once);
            radio.Verify(r => r.SendTrackFinishedAsync("42", 10), Times.Once);
            radio.Verify(r => r.SendTrackSkippedAsync("42", 5), Times.Once);
        });
    }

    private static YandexMusicSource CreateSource(Mock<IYandexRadioService> radio) =>
        new(new YandexMusicApi(), new AuthStorage(), "tracks", "covers", Mock.Of<IYandexFavoriteService>(), radio.Object);

    private static Track Track(string id) =>
        new($"T-{id}", "A", "Album", id) { SourceId = YandexMusicSourceId.Value };
}
