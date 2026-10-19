using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class TracksPresenter : ITracksPresenter
{
	private readonly ITracksView _view;
	private readonly ITrackFileProvider _trackFileProvider;
	private readonly ITrackRepository _trackRepository;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IEventBus _eventBus;
	private readonly ITrackDownloadTracker _downloadTracker;
	private readonly Action<TrackChangedEvent> _onTrackChangedHandler;

	private List<Track> _tracks = new();

	/// <summary>Выделение последнего игравшего трека восстанавливаем только при первом показе списка.</summary>
	private bool _restoreSelectionPending = true;

	private const int TracksPerBatch = 100;

	public event Action<Track>? OnTrackChosen;

	public IPlaybackQueue PlaybackQueue => _playbackQueue;

	public TracksPresenter(ITracksView view, ITrackFileProvider trackFileProvider, ITrackRepository trackRepository, IPlaybackQueue playbackQueue, IEventBus eventBus, ITrackDownloadTracker downloadTracker)
	{
		_view = view;
		_trackFileProvider = trackFileProvider;
		_trackRepository = trackRepository;
		_playbackQueue = playbackQueue;
		_eventBus = eventBus;
		_downloadTracker = downloadTracker;

		_view.OnTrackSelected += OnTrackSelected;
		_view.NeedMoreTracks += () => OnNeedMoreTracks().Forget();
		_view.OnCellActivated += ViewOnTrackSelected;
		_onTrackChangedHandler = e => OnPlayingTrackChanged(e.TrackId);
		_eventBus.Subscribe(_onTrackChangedHandler);

		_downloadTracker.Changed += OnDownloadChanged;
	}

	public async Task LoadTracksFor(Playlist playlist)
	{
		Logging.LogBeforeCall();

		await _trackRepository.SetPlaylist(playlist);

		IEnumerable<Track> trackBatch = await _trackRepository.GetCachedTracksOrMinimum(TracksPerBatch);
		_tracks = trackBatch.ToList();

		if (_tracks.Count == 0)
		{
			_view.ClearTracks();
		}
		else
		{
			_view.SetTracks(_tracks, _trackFileProvider.IsTrackDownloaded);
			RestoreLastPlayedSelection();
		}

		_view.SetPlayingTrackId(_playbackQueue.CurrentTrackId);
		ReplayActiveDownloads();
		Logging.LogAfterCall();
	}

	/// <summary>
	/// Состояние загрузки приходит из потока скачивания; в UI-поток его переводит
	/// сама вьюха. Ошибки загрузки видны в строке трека, пока трек не перезагрузят.
	/// </summary>
	private void OnDownloadChanged(string trackId, TrackDownloadState state)
		=> _view.SetDownloadState(trackId, state);

	/// <summary>
	/// Список могли пересобрать прямо во время загрузки — досылаем активные состояния,
	/// иначе прогресс пропадёт до следующего отчёта.
	/// </summary>
	private void ReplayActiveDownloads()
	{
		foreach ((string trackId, TrackDownloadState state) in _downloadTracker.GetActive())
			_view.SetDownloadState(trackId, state);
	}

	private void ViewOnTrackSelected(int trackNumber)
	{
		var allTrackIds = _trackRepository.GetAllTrackIds();
		if (trackNumber < 0 || trackNumber >= allTrackIds.Count)
			return;

		Logging.LogUserAction($"воспроизведение трека №{trackNumber + 1} из списка");
		_playbackQueue.SetQueue(allTrackIds, trackNumber);
	}

	private void OnPlayingTrackChanged(string trackId)
	{
		_trackRepository.SetLastPlayedTrack(trackId);
		_view.SetPlayingTrackId(trackId);
	}

	/// <summary>
	/// При запуске выделяем трек, который играл в прошлый раз, — чтобы продолжить
	/// с того же места списка. Дальше выделение живёт своей жизнью.
	/// </summary>
	private void RestoreLastPlayedSelection()
	{
		if (!_restoreSelectionPending)
			return;

		_restoreSelectionPending = false;

		string? lastTrackId = _trackRepository.GetPersistedSnapshot()?.LastTrackId;
		if (!string.IsNullOrWhiteSpace(lastTrackId))
			_view.SelectTrack(lastTrackId);
	}

	private void OnTrackSelected(int index)
	{
		if (index < 0 || index >= _tracks.Count)
		{
			return;
		}

		OnTrackChosen?.Invoke(_tracks[index]);
	}

	private async Task OnNeedMoreTracks()
	{
		IEnumerable<Track> result = await _trackRepository.GetNextTracks(TracksPerBatch);
		var more = result.ToList();
		if (more.Count == 0)
		{
			return;
		}

		_tracks.AddRange(more);
		_view.AddTracks(more, _trackFileProvider.IsTrackDownloaded);
		ReplayActiveDownloads();
	}
}