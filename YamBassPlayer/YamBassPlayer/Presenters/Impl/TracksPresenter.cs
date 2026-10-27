using System.Threading;
using Serilog;
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

	private readonly object _tracksSync = new();
	private List<Track> _tracks = new();
	private Playlist? _currentPlaylist;

	/// <summary>Трек, выделенный пользователем: переживает перезагрузку списка.</summary>
	private TrackKey? _selectedTrack;

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
		_onTrackChangedHandler = e => OnPlayingTrackChanged(e.Key);
		_eventBus.Subscribe(_onTrackChangedHandler);
		_eventBus.Subscribe((PlaylistCompositionUpdatedEvent e) => OnCompositionUpdated(e));

		_downloadTracker.Changed += OnDownloadChanged;
	}

	/// <inheritdoc />
	public async Task LoadTracksFor(Playlist playlist, CancellationToken ct = default)
	{
		Logging.LogBeforeCall();

		lock (_tracksSync)
		{
			_currentPlaylist = playlist;
		}

		_view.SetState(TrackListState.Loading);

		// Состав берётся из памяти/БД, в источник идём только для невиданного плейлиста.
		await _trackRepository.SetPlaylist(playlist, ct);
		ct.ThrowIfCancellationRequested();

		List<Track> cached = (await _trackRepository.GetCachedTracks(TracksPerBatch, ct)).ToList();
		int expected = Math.Min(TracksPerBatch, _trackRepository.GetAllTrackIds().Count);
		ApplyTracks(cached, moreExpected: expected > cached.Count);
		_view.SetPlayingTrack(_playbackQueue.CurrentTrack);
		ReplayActiveDownloads();

		Logging.LogAfterCall();

		// Метаданные, которых нет в БД, догружаем в фоне: список уже на экране.
		_ = EnrichAsync(playlist, ct);
	}

	/// <summary>
	/// Догружает недостающие метаданные первых треков плейлиста. Ошибка не ломает
	/// список: показываем то, что уже есть в БД.
	/// </summary>
	private async Task EnrichAsync(Playlist playlist, CancellationToken ct)
	{
		try
		{
			int expected = Math.Min(TracksPerBatch, _trackRepository.GetAllTrackIds().Count);
			int shown;
			lock (_tracksSync)
			{
				shown = _tracks.Count;
			}

			if (shown >= expected)
				return;

			IReadOnlyList<Track> loaded = await _trackRepository.LoadTracksInfo(TracksPerBatch, ct);
			ct.ThrowIfCancellationRequested();

			if (!IsCurrent(playlist))
				return;

			int currentCount;
			lock (_tracksSync)
			{
				currentCount = _tracks.Count;
			}

			// Пока метаданные догружались, список могли расширить пагинацией:
			// не откатываем его к первой порции.
			if (currentCount != shown)
				return;

			if (loaded.Count > shown)
			{
				ApplyTracks(loaded.ToList());
				ReplayActiveDownloads();
			}
			else if (shown == 0)
			{
				// Догрузить нечего — плейлист действительно пуст (или источник не ответил).
				_view.SetState(TrackListState.Ready);
			}
		}
		catch (OperationCanceledException)
		{
			// Пользователь выбрал другой плейлист — догрузка больше не нужна.
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Не удалось догрузить метаданные треков плейлиста «{Title}»", playlist.Title);
		}
	}

	/// <summary>
	/// Состав текущего плейлиста изменился в источнике: перечитываем список из БД,
	/// сохраняя выделение пользователя.
	/// </summary>
	private void OnCompositionUpdated(PlaylistCompositionUpdatedEvent e)
	{
		Playlist? current;
		lock (_tracksSync)
		{
			current = _currentPlaylist;
		}

		if (current is null
			|| current.SourceId != e.Source
			|| !string.Equals(current.CacheKey, e.PlaylistKey, StringComparison.Ordinal))
		{
			return;
		}

		_ = ReloadCompositionAsync(current);
	}

	private async Task ReloadCompositionAsync(Playlist playlist)
	{
		try
		{
			List<Track> cached = (await _trackRepository.GetCachedTracks(TracksPerBatch)).ToList();
			if (!IsCurrent(playlist))
				return;

			int expected = Math.Min(TracksPerBatch, _trackRepository.GetAllTrackIds().Count);
			ApplyTracks(cached, moreExpected: expected > cached.Count);
			ReplayActiveDownloads();

			if (expected > cached.Count)
				_ = EnrichAsync(playlist, CancellationToken.None);
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Не удалось перечитать состав плейлиста «{Title}»", playlist.Title);
		}
	}

	private bool IsCurrent(Playlist playlist)
	{
		lock (_tracksSync)
		{
			return _currentPlaylist is not null && _currentPlaylist.HasSameIdentity(playlist);
		}
	}

	private void ApplyTracks(List<Track> tracks, bool moreExpected = false)
	{
		lock (_tracksSync)
		{
			_tracks = tracks;
		}

		if (tracks.Count == 0)
		{
			_view.ClearTracks();
			// Если метаданные ещё догружаются, «пусто» показывать рано.
			_view.SetState(moreExpected ? TrackListState.Loading : TrackListState.Ready);
			return;
		}

		_view.SetTracks(tracks, _trackFileProvider.IsTrackDownloaded);
		RestoreSelection();
	}

	/// <summary>
	/// Состояние загрузки приходит из потока скачивания; в UI-поток его переводит
	/// сама вьюха. Ошибки загрузки видны в строке трека, пока трек не перезагрузят.
	/// </summary>
	private void OnDownloadChanged(TrackKey key, TrackDownloadState state)
		=> _view.SetDownloadState(key, state);

	/// <summary>
	/// Список могли пересобрать прямо во время загрузки — досылаем активные состояния,
	/// иначе прогресс пропадёт до следующего отчёта.
	/// </summary>
	private void ReplayActiveDownloads()
	{
		foreach ((TrackKey key, TrackDownloadState state) in _downloadTracker.GetActive())
			_view.SetDownloadState(key, state);
	}

	private void ViewOnTrackSelected(int trackNumber)
	{
		var allTrackIds = _trackRepository.GetAllTrackIds();
		if (trackNumber < 0 || trackNumber >= allTrackIds.Count)
			return;

		lock (_tracksSync)
		{
			_selectedTrack = allTrackIds[trackNumber];
		}

		Logging.LogUserAction($"воспроизведение трека №{trackNumber + 1} из списка");
		_playbackQueue.SetQueue(allTrackIds, trackNumber);
	}

	private void OnPlayingTrackChanged(TrackKey key)
	{
		_trackRepository.SetLastPlayedTrack(key);
		_view.SetPlayingTrack(key);
	}

	/// <summary>
	/// После перезагрузки списка возвращаем выделение: сначала на трек, который выбрал
	/// пользователь, иначе — на последний игравший. Фоновое обновление состава
	/// перезагружает список, и без этого выделение сбрасывалось на первую строку.
	/// </summary>
	private void RestoreSelection()
	{
		List<Track> tracks;
		TrackKey? selected;
		lock (_tracksSync)
		{
			tracks = _tracks.ToList();
			selected = _selectedTrack;
		}

		if (selected is { } selectedKey)
		{
			_view.SelectTrack(selectedKey);
			return;
		}

		// Снимок хранит ключ трека; восстанавливаем выделение по уже загруженному списку.
		TrackKey? lastTrack = _trackRepository.GetPersistedSnapshot()?.LastTrack;
		if (lastTrack is null)
			return;

		Track? track = tracks.FirstOrDefault(t => t.Key == lastTrack.Value);
		if (track is not null)
			_view.SelectTrack(track.Key);
	}

	private void OnTrackSelected(int index)
	{
		Track? track = null;
		lock (_tracksSync)
		{
			if (index >= 0 && index < _tracks.Count)
			{
				track = _tracks[index];
				_selectedTrack = track.Key;
			}
		}

		if (track is not null)
			OnTrackChosen?.Invoke(track);
	}

	private async Task OnNeedMoreTracks()
	{
		IEnumerable<Track> result = await _trackRepository.GetNextTracks(TracksPerBatch);
		var more = result.ToList();
		if (more.Count == 0)
		{
			return;
		}

		lock (_tracksSync)
		{
			_tracks.AddRange(more);
		}

		_view.AddTracks(more, _trackFileProvider.IsTrackDownloaded);
		ReplayActiveDownloads();
	}
}
