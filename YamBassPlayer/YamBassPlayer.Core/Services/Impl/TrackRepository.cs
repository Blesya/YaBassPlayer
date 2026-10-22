using System.Threading;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

public class TrackRepository : ITrackRepository
{
	private readonly IMusicSourceRegistry _musicSourceRegistry;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly IHistoryService _historyService;
	private readonly ITrackRepositoryCache _cache;
	private readonly ILocalLibraryService _localLibraryService;
	private readonly PlaylistLoadStrategyResolver _strategyResolver;
	private readonly IAppPlaylistProvider _appPlaylistProvider;
	private readonly IPlaylistStateStore _playlistStateStore;

	private List<TrackKey> _trackIds = new();
	private Playlist? _currentPlaylist;
	private int _currentOffset = 0;
	private PlaylistState? _persistedState;
	private bool _persistedStateLoaded;

	public TrackRepository(
		IMusicSourceRegistry musicSourceRegistry,
		ITrackInfoProvider trackInfoProvider,
		IHistoryService historyService,
		ITrackRepositoryCache cache,
		ILocalLibraryService localLibraryService,
		PlaylistLoadStrategyResolver strategyResolver,
		IAppPlaylistProvider appPlaylistProvider,
		IPlaylistStateStore playlistStateStore)
	{
		_musicSourceRegistry = musicSourceRegistry;
		_trackInfoProvider = trackInfoProvider;
		_historyService = historyService;
		_cache = cache;
		_localLibraryService = localLibraryService;
		_strategyResolver = strategyResolver;
		_appPlaylistProvider = appPlaylistProvider;
		_playlistStateStore = playlistStateStore;

		_cache.MyWaveReplaced += OnMyWaveReplaced;
		_cache.MyWaveAppended += OnMyWaveAppended;
	}

	public PlaylistState? GetPersistedSnapshot()
	{
		if (_persistedStateLoaded)
			return _persistedState;

		_persistedStateLoaded = true;
		_persistedState = _playlistStateStore.Load();
		return _persistedState;
	}

	public async Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default)
	{
		Logging.LogBeforeCall();

		ct.ThrowIfCancellationRequested();
		try
		{
			var playlists = new List<Playlist>();
			foreach (IMusicSource source in _musicSourceRegistry.Sources)
			{
				// Неготовый источник (например, Яндекс без токена) не дёргаем:
				// его плейлисты остаются в снимке до успешного входа.
				if (source.State != SourceState.Ready)
					continue;

				foreach (Playlist playlist in await source.GetPlaylistsAsync(ct))
				{
					if (playlist.Category != PlaylistCategory.Data)
						continue;

					if (playlist.SourceId is null)
					{
						Log.Warning("Плейлист {Title} объявлен как Data без источника и не попал в дерево", playlist.Title);
						continue;
					}

					playlists.Add(playlist);
				}
			}

			playlists.AddRange(await _appPlaylistProvider.GetAppPlaylistsAsync(ct));

			PersistState(playlists);
			Logging.LogAfterCall();
			return playlists;
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}

	/// <summary>Сохраняет актуальный состав плейлистов источников для следующего запуска.</summary>
	private void PersistState(IReadOnlyList<Playlist> playlists)
	{
		List<Playlist> current = playlists
			.Where(p => p.Category == PlaylistCategory.Data)
			.ToList();

		// Источник может быть ещё не готов (ждёт вход) и не отдать свои плейлисты.
		// Тогда не выкидываем их из снимка — иначе прошлое состояние теряется.
		PlaylistState? previous = GetPersistedSnapshot();
		if (previous is { Playlists.Count: > 0 })
		{
			var readySources = _musicSourceRegistry.Sources
				.Where(source => source.State == SourceState.Ready)
				.Select(source => source.Id.Value)
				.ToHashSet(StringComparer.Ordinal);

			foreach (Playlist old in previous.Playlists)
			{
				if (old.SourceId is { } sourceId
					&& !readySources.Contains(sourceId.Value)
					&& !current.Any(existing => existing.HasSameIdentity(old)))
				{
					current.Add(old);
				}
			}
		}

		_persistedState = new PlaylistState
		{
			Playlists = current,
			LastPlaylist = previous?.LastPlaylist ?? _currentPlaylist,
			LastTrack = previous?.LastTrack,
			SavedAt = DateTime.UtcNow
		};
		_persistedStateLoaded = true;
		_playlistStateStore.Save(_persistedState);
	}

	/// <summary>Запоминает выбранный плейлист, чтобы восстановить выделение при следующем запуске.</summary>
	private void PersistLastPlaylist(Playlist playlist)
	{
		// Временные плейлисты (поиск, очередь, «Моя волна») не переживают перезапуск.
		if (playlist.Category == PlaylistCategory.Transient)
			return;

		var state = _persistedState;
		if (state is null || state.Playlists.Count == 0)
			return;

		if (state.LastPlaylist is { } last && last.HasSameIdentity(playlist))
			return;

		_persistedState = new PlaylistState
		{
			Playlists = state.Playlists,
			LastPlaylist = playlist,
			LastTrack = state.LastTrack,
			SavedAt = DateTime.UtcNow
		};
		_playlistStateStore.Save(_persistedState);
	}

	/// <inheritdoc />
	public void SetLastPlayedTrack(TrackKey key)
	{
		if (string.IsNullOrWhiteSpace(key.TrackId))
			return;

		var state = _persistedState;
		if (state is null || state.LastTrack == key)
			return;

		_persistedState = new PlaylistState
		{
			Playlists = state.Playlists,
			LastPlaylist = state.LastPlaylist,
			LastTrack = key,
			SavedAt = DateTime.UtcNow
		};
		_playlistStateStore.Save(_persistedState);
	}

	public async Task SetPlaylist(Playlist playlist, CancellationToken ct = default)
	{
		Logging.LogBeforeCall();

		ct.ThrowIfCancellationRequested();
		try
		{
			List<TrackKey> trackKeys = await LoadTrackKeysAsync(playlist, ct);

			_trackIds = trackKeys;
			_currentOffset = 0;
			_currentPlaylist = playlist;

			PersistLastPlaylist(playlist);
			Logging.LogAfterCall();
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
	}

	private async Task<List<TrackKey>> LoadTrackKeysAsync(Playlist playlist, CancellationToken ct)
	{
		if (playlist.SourceId is null)
			return await _strategyResolver.Resolve(playlist.Kind).LoadTrackIdsAsync(playlist);

		if (playlist.Category == PlaylistCategory.Transient)
		{
			if (playlist.Context == PlaylistContext.Radio)
				return _cache.MyWaveTracks.Select(track => track.Key).ToList();

			return _cache.TryGetSearchTrackIds(playlist.SourceId.Value, out var cached)
				? cached.ToList()
				: [];
		}

		IMusicSource source = _musicSourceRegistry.GetRequired(playlist.SourceId.Value);
		IReadOnlyList<string> ids = await source.GetPlaylistTrackIdsAsync(playlist);
		return ids.Select(id => new TrackKey(playlist.SourceId.Value, id)).ToList();
	}

	public async Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			int start = Math.Clamp(_currentOffset, 0, _trackIds.Count);
			int count = Math.Clamp(_trackIds.Count - start, 0, Math.Max(0, tracksPerBatch));
			var slice = _trackIds.GetRange(start, count);

			_currentOffset += tracksPerBatch;

			List<Track> tracksResult = new List<Track>();

			IEnumerable<Track> tracks = await _trackInfoProvider.GetTracksInfoByIds(slice);

			tracksResult.AddRange(tracks);

			return tracksResult;
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}

	public IReadOnlyList<TrackKey> GetAllTrackIds() => _trackIds.AsReadOnly();

	private void OnMyWaveReplaced()
	{
		if (_currentPlaylist?.Context == PlaylistContext.Radio)
		{
			_trackIds.Clear();
			_trackIds.AddRange(_cache.MyWaveTracks.Select(t => t.Key));
		}
	}

	private void OnMyWaveAppended()
	{
		if (_currentPlaylist?.Context == PlaylistContext.Radio)
			_trackIds.AddRange(_cache.MyWaveTracks.Select(t => t.Key));
	}

	public async Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			if (_currentPlaylist?.Context == PlaylistContext.Radio)
			{
				_currentOffset = _cache.MyWaveTracks.Count;
				return _cache.MyWaveTracks.ToList();
			}

			int cachedCount = await _trackInfoProvider.CountCachedTracks(_trackIds);

			int countToLoad = Math.Max(cachedCount, minCount);
			countToLoad = Math.Min(countToLoad, _trackIds.Count);

			var keysToLoad = _trackIds.Take(countToLoad).ToList();
			_currentOffset = countToLoad;

			return await _trackInfoProvider.GetTracksInfoByIds(keysToLoad);
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}
}
