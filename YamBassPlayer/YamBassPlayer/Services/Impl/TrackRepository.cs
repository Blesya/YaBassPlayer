using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

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
	private readonly IYandexPlaylistInitializer _yandexPlaylistInitializer;
	private readonly IPlaylistStateStore _playlistStateStore;

	private IMusicSource YandexSource => _musicSourceRegistry.GetRequired(SourceIds.Yandex);
	private List<string> _tracksIds = new();
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
		IYandexPlaylistInitializer yandexPlaylistInitializer,
		IPlaylistStateStore playlistStateStore)
	{
		_musicSourceRegistry = musicSourceRegistry;
		_trackInfoProvider = trackInfoProvider;
		_historyService = historyService;
		_cache = cache;
		_localLibraryService = localLibraryService;
		_strategyResolver = strategyResolver;
		_appPlaylistProvider = appPlaylistProvider;
		_yandexPlaylistInitializer = yandexPlaylistInitializer;
		_playlistStateStore = playlistStateStore;

		_cache.MyWaveReplaced += OnMyWaveReplaced;
		_cache.MyWaveAppended += OnMyWaveAppended;
	}

	public PlaylistType? CurrentPlaylistType => _currentPlaylist?.Type;

	public PlaylistState? GetPersistedSnapshot()
	{
		if (_persistedStateLoaded)
			return _persistedState;

		_persistedStateLoaded = true;
		_persistedState = _playlistStateStore.Load();

		if (_persistedState is not null)
		{
			// Наполняем кэши составом из прошлого запуска, чтобы выбранный
			// плейлист отрисовался мгновенно, без обращения к сети.
			_cache.ReplaceFavoriteTrackIds(_persistedState.FavoriteTrackIds);
			foreach (var (playlistName, trackIds) in _persistedState.CustomPlaylistTrackIds)
				_cache.SetCustomPlaylistIds(playlistName, trackIds);
		}

		return _persistedState;
	}

	public async Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			var yandexPlaylists = (await YandexSource.GetPlaylistsAsync(ct)).ToList();
			await _yandexPlaylistInitializer.InitializeAsync(yandexPlaylists, ct);

			var appPlaylists = await _appPlaylistProvider.GetAppPlaylistsAsync(ct);

			var result = appPlaylists
				.Concat(yandexPlaylists.Where(p => p.Type is PlaylistType.Custom or PlaylistType.Favorite))
				.ToList();

			PersistState(result, yandexPlaylists);
			return result;
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}

	/// <summary>Сохраняет актуальный состав плейлистов для следующего запуска.</summary>
	private void PersistState(IReadOnlyList<Playlist> playlists, IReadOnlyList<Playlist> yandexPlaylists)
	{
		var customPlaylistTrackIds = new Dictionary<string, List<string>>();
		foreach (var playlist in yandexPlaylists.Where(p => p.Type == PlaylistType.Custom))
		{
			if (_cache.TryGetCustomPlaylistIds(playlist.PlaylistName, out var trackIds))
				customPlaylistTrackIds[playlist.PlaylistName] = trackIds.ToList();
		}

		_persistedState = new PlaylistState
		{
			Playlists = playlists.ToList(),
			FavoriteTrackIds = _cache.FavoriteTrackIds.ToList(),
			CustomPlaylistTrackIds = customPlaylistTrackIds,
			LastPlaylist = _persistedState?.LastPlaylist ?? _currentPlaylist,
			SavedAt = DateTime.UtcNow
		};
		_persistedStateLoaded = true;
		_playlistStateStore.Save(_persistedState);
	}

	/// <summary>Запоминает выбранный плейлист, чтобы восстановить выделение при следующем запуске.</summary>
	private void PersistLastPlaylist(Playlist playlist)
	{
		// Временные плейлисты (поиск, очередь, «Моя волна») не переживают перезапуск — не сохраняем их.
		if (playlist.Type.GetCategory() == PlaylistCategory.Transient)
			return;

		var state = _persistedState;
		if (state is null || state.Playlists.Count == 0)
			return;

		var last = state.LastPlaylist;
		if (last is not null
			&& last.Type == playlist.Type
			&& last.PlaylistName == playlist.PlaylistName)
		{
			return;
		}

		_persistedState = new PlaylistState
		{
			Playlists = state.Playlists,
			FavoriteTrackIds = state.FavoriteTrackIds,
			CustomPlaylistTrackIds = state.CustomPlaylistTrackIds,
			LastPlaylist = playlist,
			SavedAt = DateTime.UtcNow
		};
		_playlistStateStore.Save(_persistedState);
	}

	public async Task SetPlaylist(Playlist playlist, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			var strategy = _strategyResolver.Resolve(playlist.Type);
			var trackIds = await strategy.LoadTrackIdsAsync(playlist);

			_tracksIds = trackIds;
			_currentOffset = 0;
			_currentPlaylist = playlist;

			PersistLastPlaylist(playlist);
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
	}

	public async Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			int start = Math.Clamp(_currentOffset, 0, _tracksIds.Count);
			int count = Math.Clamp(_tracksIds.Count - start, 0, Math.Max(0, tracksPerBatch));
			var slice = _tracksIds.GetRange(start, count);

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

	public IReadOnlyList<string> GetAllTrackIds() => _tracksIds.AsReadOnly();

	private void OnMyWaveReplaced()
	{
		if (_currentPlaylist?.Type == PlaylistType.MyWave)
		{
			_tracksIds.Clear();
			_tracksIds.AddRange(_cache.MyWaveTracks.Select(t => t.Id));
		}
	}

	private void OnMyWaveAppended()
	{
		if (_currentPlaylist?.Type == PlaylistType.MyWave)
			_tracksIds.AddRange(_cache.MyWaveTracks.Select(t => t.Id));
	}

	public async Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount, CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			if (_currentPlaylist?.Type == PlaylistType.MyWave)
			{
				_currentOffset = _cache.MyWaveTracks.Count;
				return _cache.MyWaveTracks.ToList();
			}

			int cachedCount = await _trackInfoProvider.CountCachedTracks(_tracksIds);

			int countToLoad = Math.Max(cachedCount, minCount);
			countToLoad = Math.Min(countToLoad, _tracksIds.Count);

			var idsToLoad = _tracksIds.Take(countToLoad).ToList();
			_currentOffset = countToLoad;

			return await _trackInfoProvider.GetTracksInfoByIds(idsToLoad);
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}
}


