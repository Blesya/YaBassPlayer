using Moq;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class PlayStatusPresenterTests
{
    private static TrackKey K(string id) => new(YandexMusicSourceId.Value, id);

    private Mock<IPlayStatusView> _view = null!;
    private Mock<IAudioPlayer> _audioPlayer = null!;
    private TrackActionRegistry _actions = null!;
    private Mock<ITrackCatalog> _catalog = null!;
    private static readonly TrackKey LocalKey = new(LocalMusicSource.Id, "/music/a.mp3");

    [SetUp]
    public void SetUp()
    {
        _view = new Mock<IPlayStatusView>();
        _audioPlayer = new Mock<IAudioPlayer>();
        _actions = new TrackActionRegistry();
        _catalog = new Mock<ITrackCatalog>();
    }

    private PlayStatusPresenter CreatePresenter()
        => new(_view.Object, _audioPlayer.Object, _actions, _catalog.Object);

    // ── Проброс событий вью → презентер ───────────────────────────────────

    [Test]
    public void ViewPlayClicked_RaisesPresenterEvent()
    {
        var presenter = CreatePresenter();
        bool raised = false;
        presenter.OnPlayClicked += () => raised = true;

        _view.Raise(v => v.OnPlayClicked += null);

        Assert.That(raised, Is.True);
    }

    [Test]
    public void ViewSeekRequested_SeeksAudioToPercent()
    {
        _ = CreatePresenter();

        _view.Raise(v => v.OnSeekRequested += null, 42);

        _audioPlayer.Verify(a => a.SeekToPercent(42), Times.Once);
    }

    [Test]
    public void Constructor_AppliesVolumeFromAudioPlayer()
    {
        _audioPlayer.Setup(a => a.VolumePercent).Returns(70);

        _ = CreatePresenter();

        _view.Verify(v => v.SetVolume(70), Times.AtLeastOnce);
    }

    [Test]
    public void ViewVolumeChangeRequested_UpdatesAudioPlayerAndView()
    {
        _ = CreatePresenter();
        _audioPlayer.Setup(a => a.VolumePercent).Returns(40);

        _view.Raise(v => v.OnVolumeChangeRequested += null, 40);

        Assert.Multiple(() =>
        {
            _audioPlayer.Verify(a => a.SetVolumePercent(40), Times.Once);
            _view.Verify(v => v.SetVolume(40), Times.AtLeastOnce);
        });
    }

    [Test]
    public void ViewQueueModeAndRestartEvents_AreBubbled()
    {
        var presenter = CreatePresenter();
        bool queue = false, mode = false, restart = false;
        presenter.OnQueueClicked += () => queue = true;
        presenter.OnPlaybackModeToggled += () => mode = true;
        presenter.OnRestartClicked += () => restart = true;

        _view.Raise(v => v.OnQueueClicked += null);
        _view.Raise(v => v.OnPlaybackModeToggled += null);
        _view.Raise(v => v.OnRestartClicked += null);

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.True);
            Assert.That(mode, Is.True);
            Assert.That(restart, Is.True);
        });
    }

    // ── Действия плагинов над текущим треком ──────────────────────────────

    [Test]
    public void SetCurrentTrack_ShowsAvailableAction()
    {
        _actions.Add(new FakeTrackAction("local-like", LocalMusicSource.Id));
        var presenter = CreatePresenter();

        presenter.SetCurrentTrack(LocalKey);

        _view.Verify(v => v.SetTrackActions(It.Is<IReadOnlyList<TrackActionButton>>(buttons =>
            buttons.Count == 1 && buttons[0].Id == "local-like" && buttons[0].Title == "В избранное")), Times.Once);
    }

    [Test]
    public void SetCurrentTrack_ForeignSource_HidesAction()
    {
        _actions.Add(new FakeTrackAction("local-like", LocalMusicSource.Id));
        var presenter = CreatePresenter();

        presenter.SetCurrentTrack(K("12345"));

        _view.Verify(v => v.SetTrackActions(It.Is<IReadOnlyList<TrackActionButton>>(buttons => buttons.Count == 0)), Times.AtLeastOnce);
    }

    [Test]
    public void TrackActionClicked_ExecutesActionAndRefreshesButtons()
    {
        var action = new FakeTrackAction("local-like", LocalMusicSource.Id);
        _actions.Add(action);
        var presenter = CreatePresenter();
        presenter.SetCurrentTrack(LocalKey);

        _view.Raise(v => v.OnTrackActionClicked += null, "local-like");

        Assert.Multiple(() =>
        {
            Assert.That(action.Executions, Is.EqualTo(1));
            _view.Verify(v => v.SetTrackActions(It.Is<IReadOnlyList<TrackActionButton>>(buttons =>
                buttons.Count == 1 && buttons[0].Title == "Из избранного")), Times.AtLeastOnce);
        });
    }

    [Test]
    public void TrackActionClicked_WithNoCurrentTrack_DoesNothing()
    {
        var action = new FakeTrackAction("local-like", LocalMusicSource.Id);
        _actions.Add(action);
        _ = CreatePresenter();

        _view.Raise(v => v.OnTrackActionClicked += null, "local-like");

        Assert.That(action.Executions, Is.Zero);
    }

    [Test]
    public void SetCurrentTrack_LoadsFullTrackForActions()
    {
        var action = new FakeTrackAction("local-like", LocalMusicSource.Id);
        _actions.Add(action);
        _catalog.Setup(c => c.GetAsync(LocalKey))
            .ReturnsAsync(new Track("Название", "Исполнитель", "Альбом", "/music/a.mp3")
            {
                SourceId = LocalMusicSource.Id
            });
        var presenter = CreatePresenter();

        presenter.SetCurrentTrack(LocalKey);

        Assert.That(action.LastTrack?.Title, Is.EqualTo("Название"));
    }

    private sealed class FakeTrackAction(string id, SourceId source) : ITrackAction
    {
        public string Id => id;
        public int Executions { get; private set; }
        public bool IsFavorite { get; private set; }
        public Track? LastTrack { get; private set; }
        public event Action? Changed;
        public bool IsAvailable(Track track)
        {
            LastTrack = track;
            return track.SourceId == source;
        }

        public string GetTitle(Track track) => IsFavorite ? "Из избранного" : "В избранное";

        public Task ExecuteAsync(Track track)
        {
            Executions++;
            IsFavorite = !IsFavorite;
            Changed?.Invoke();
            return Task.CompletedTask;
        }
    }

    // ── Прочие делегирования ──────────────────────────────────────────────

    [Test]
    public void SetPlaybackMode_DelegatesToView()
    {
        var presenter = CreatePresenter();

        presenter.SetPlaybackMode(PlaybackMode.Shuffle);

        _view.Verify(v => v.SetPlaybackMode(PlaybackMode.Shuffle), Times.Once);
    }

    [Test]
    public void SetPlayStatusAndTitle_DelegateToView()
    {
        var presenter = CreatePresenter();

        presenter.SetPlayStatus("статус");
        presenter.SetTitle("заголовок");

        Assert.Multiple(() =>
        {
            _view.Verify(v => v.SetPlayStatus("статус"), Times.Once);
            _view.Verify(v => v.SetTitle("заголовок"), Times.Once);
        });
    }
}
