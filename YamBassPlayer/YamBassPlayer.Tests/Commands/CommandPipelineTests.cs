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
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

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
        repo.Setup(r => r.GetAllTrackIds()).Returns(new[] { K("a"), K("b") });
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
        repo.Setup(r => r.GetAllTrackIds()).Returns(new[] { K("a"), K("b"), K("c") });
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

    // ──────────────── ModeCommand ────────────────

    [TestCase("seq", PlaybackMode.Sequential)]
    [TestCase("shuffle", PlaybackMode.Shuffle)]
    [TestCase("repeat", PlaybackMode.Repeat)]
    [TestCase("повтор", PlaybackMode.Repeat)]
    public void ModeCommand_PublishesRequestedMode(string argument, PlaybackMode expected)
    {
        var bus = new EventBus();
        PlaybackModeCommandEvent? received = null;
        bus.Subscribe((PlaybackModeCommandEvent e) => received = e);

        var result = new ModeCommand(bus).Execute(new[] { argument });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received?.Mode, Is.EqualTo(expected));
        });
    }

    [Test]
    public void ModeCommand_UnknownMode_ReturnsError()
    {
        var bus = new EventBus();
        PlaybackModeCommandEvent? received = null;
        bus.Subscribe((PlaybackModeCommandEvent e) => received = e);

        var result = new ModeCommand(bus).Execute(new[] { "loop" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(received, Is.Null);
        });
    }

    // ──────────────── VolumeCommand ────────────────

    [Test]
    public void VolumeCommand_WithoutArgs_ReportsCurrentVolume()
    {
        var audio = new Mock<IAudioPlayer>();
        audio.Setup(a => a.VolumePercent).Returns(70);

        var result = new VolumeCommand(audio.Object).Execute([]);

        Assert.That(result.Message, Does.Contain("70%"));
        audio.Verify(a => a.SetVolumePercent(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void VolumeCommand_WithAbsoluteValue_SetsVolume()
    {
        var audio = new Mock<IAudioPlayer>();

        _ = new VolumeCommand(audio.Object).Execute(new[] { "35" });

        audio.Verify(a => a.SetVolumePercent(35), Times.Once);
    }

    [Test]
    public void VolumeCommand_WithRelativeValue_ChangesVolume()
    {
        var audio = new Mock<IAudioPlayer>();
        audio.Setup(a => a.VolumePercent).Returns(70);

        _ = new VolumeCommand(audio.Object).Execute(new[] { "-15" });

        audio.Verify(a => a.SetVolumePercent(55), Times.Once);
    }

    [Test]
    public void VolumeCommand_ClampsOutOfRangeValue()
    {
        var audio = new Mock<IAudioPlayer>();

        _ = new VolumeCommand(audio.Object).Execute(new[] { "150" });

        audio.Verify(a => a.SetVolumePercent(100), Times.Once);
    }

    [Test]
    public void VolumeCommand_WithGarbage_ReturnsError()
    {
        var audio = new Mock<IAudioPlayer>();

        var result = new VolumeCommand(audio.Object).Execute(new[] { "громче" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            audio.Verify(a => a.SetVolumePercent(It.IsAny<int>()), Times.Never);
        });
    }

    // ──────────────── SearchCommand: локальный поиск ────────────────

    [Test]
    public void SearchCommand_PublishesLocalSourceAndQuery()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus, LocalMusicSource.Id).Execute(new[] { "Rammstein" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Source, Is.EqualTo(LocalMusicSource.Id));
            Assert.That(received.Query, Is.EqualTo("Rammstein"));
            Assert.That(received.Kind, Is.EqualTo(SearchEntityKind.Tracks));
        });
    }

    [Test]
    public void SearchCommand_WithoutText_Rejects()
    {
        var bus = new EventBus();
        SearchCommandEvent? received = null;
        bus.Subscribe((SearchCommandEvent e) => received = e);

        var result = new SearchCommand(bus, LocalMusicSource.Id).Execute([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(received, Is.Null);
        });
    }

    // ──────────────── LikeCommands: избранное адресуется ключом ────────────────

    [Test]
    public void LikeLocalCommand_CurrentTrackOfOtherSource_PublishesKey()
    {
        var bus = new EventBus();
        var key = new TrackKey(YandexMusicSourceId.Value, "123");
        var queue = new Mock<IPlaybackQueue>();
        queue.SetupGet(q => q.CurrentTrack).Returns(key);
        LikeCommandEvent? received = null;
        bus.Subscribe((LikeCommandEvent e) => received = e);

        var result = new LikeLocalCommand(queue.Object, bus).Execute([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Key, Is.EqualTo(key));
        });
    }

    [Test]
    public void LikeLocalCommand_CurrentTrackOfLocalSource_PublishesKey()
    {
        var bus = new EventBus();
        var key = new TrackKey(LocalMusicSource.Id, "/music/a.mp3");
        var queue = new Mock<IPlaybackQueue>();
        queue.SetupGet(q => q.CurrentTrack).Returns(key);
        LikeCommandEvent? received = null;
        bus.Subscribe((LikeCommandEvent e) => received = e);

        var result = new LikeLocalCommand(queue.Object, bus).Execute([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Key, Is.EqualTo(key));
        });
    }

    [Test]
    public void LikeCommand_WithoutCurrentTrack_Rejects()
    {
        var bus = new EventBus();
        var queue = new Mock<IPlaybackQueue>();
        queue.SetupGet(q => q.CurrentTrack).Returns((TrackKey?)null);

        var result = new LikeLocalCommand(queue.Object, bus).Execute([]);

        Assert.That(result.Success, Is.False);
    }
}