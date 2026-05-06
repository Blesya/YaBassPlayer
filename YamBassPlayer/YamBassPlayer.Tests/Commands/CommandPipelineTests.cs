namespace YamBassPlayer.Tests.Commands;

using Moq;
using YamBassPlayer.Commands;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Services.Impl;

[TestFixture]
public sealed class CommandPipelineTests
{
    // ──────────────── Команды публикуют интенты в EventBus ────────────────

    [Test]
    public void PauseCommand_PublishesPauseIntent()
    {
        var bus = new EventBus();
        PauseCommandEvent? received = null;
        bus.Subscribe((PauseCommandEvent e) => received = e);

        _ = new PauseCommand(bus).Execute([]);

        Assert.That(received, Is.Not.Null);
    }

    [Test]
    public void ResumeCommand_RejectsInvalidIndex_WithoutPublishing()
    {
        var bus = new EventBus();
        var repo = new Mock<ITrackRepository>();
        repo.Setup(r => r.GetAllTrackIds()).Returns(new[] { "a", "b" });
        PlayTrackAtCommandEvent? received = null;
        bus.Subscribe((PlayTrackAtCommandEvent e) => received = e);

        var result = new PlayCommand(bus, repo.Object).Execute(new[] { "5" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(received, Is.Null);
        });
    }

    [Test]
    public void PlayCommand_WithValidIndex_PublishesPlayTrackAtIntent()
    {
        var bus = new EventBus();
        var repo = new Mock<ITrackRepository>();
        repo.Setup(r => r.GetAllTrackIds()).Returns(new[] { "a", "b", "c" });
        PlayTrackAtCommandEvent? received = null;
        bus.Subscribe((PlayTrackAtCommandEvent e) => received = e);

        var result = new PlayCommand(bus, repo.Object).Execute(new[] { "2" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Index, Is.EqualTo(1)); // 0-based
        });
    }

    [Test]
    public void SeekCommand_RejectsOutOfRangePercent()
    {
        var bus = new EventBus();
        SeekCommandEvent? received = null;
        bus.Subscribe((SeekCommandEvent e) => received = e);

        var result = new SeekCommand(bus).Execute(new[] { "150" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(received, Is.Null);
        });
    }

    // ──────────────── SearchCommand: режимы -t / -ar / -alb ────────────────

    [Test]
    public void SearchCommand_DefaultsToTracks_ForYaPrefix()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus).Execute(new[] { "ya", "Rammstein" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Source, Is.EqualTo(SourceIds.Yandex));
            Assert.That(received.Query, Is.EqualTo("Rammstein"));
            Assert.That(received.Kind, Is.EqualTo(SearchEntityKind.Tracks));
        });
    }

    [Test]
    public void SearchCommand_WithTrackFlag_SetsTracksKind()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus).Execute(new[] { "ya", "-t", "Rammstein" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Kind, Is.EqualTo(SearchEntityKind.Tracks));
            Assert.That(received.Query, Is.EqualTo("Rammstein"));
        });
    }

    [Test]
    public void SearchCommand_WithArtistFlag_SetsArtistKind()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus).Execute(new[] { "ya", "-ar", "Rammstein" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Source, Is.EqualTo(SourceIds.Yandex));
            Assert.That(received.Kind, Is.EqualTo(SearchEntityKind.Artist));
            Assert.That(received.Query, Is.EqualTo("Rammstein"));
        });
    }

    [Test]
    public void SearchCommand_WithAlbumFlag_SetsAlbumKind()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus).Execute(new[] { "ya", "-alb", "Mutter" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Kind, Is.EqualTo(SearchEntityKind.Album));
            Assert.That(received.Query, Is.EqualTo("Mutter"));
        });
    }

    [Test]
    public void SearchCommand_ArtistFlag_WithoutYa_Rejects()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus).Execute(new[] { "-ar", "Rammstein" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(received, Is.Null);
        });
    }

    [Test]
    public void SearchCommand_AlbumFlag_WithoutYa_Rejects()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus).Execute(new[] { "-alb", "Mutter" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(received, Is.Null);
        });
    }
}
