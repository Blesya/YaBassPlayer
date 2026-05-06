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

	private IMusicSource YandexSource => _musicSourceRegistry.GetRequired(SourceIds.Yandex);
	private List<string> _tracksIds = new();
	private Playlist? _currentPlaylist;
	private int _currentOffset = 0;

	public TrackRepository(
		IMusicSourceRegistry musicSourceRegistry,
		ITrackInfoProvider trackInfoProvider,
		IHistoryService historyService,
		ITrackRepositoryCache cache,
		ILocalLibraryService localLibraryService,
		PlaylistLoadStrategyResolver strategyResolver,
		IAppPlaylistProvider appPlaylistProvider,
		IYandexPlaylistInitializer yandexPlaylistInitializer)
	{
		_musicSourceRegistry = musicSourceRegistry;
		_trackInfoProvider = trackInfoProvider;
		_historyService = historyService;
		_cache = cache;
		_localLibraryService = localLibraryService;
		_strategyResolver = strategyResolver;
		_appPlaylistProvider = appPlaylistProvider;
		_yandexPlaylistInitializer = yandexPlaylistInitializer;

		_cache.MyWaveReplaced += OnMyWaveReplaced;
		_cache.MyWaveAppended += OnMyWaveAppended;
	}

	public PlaylistType? CurrentPlaylistType => _currentPlaylist?.Type;

	public async Task<IEnumerable<Playlist>> GetPlaylists(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		try
		{
			var yandexPlaylists = (await YandexSource.GetPlaylistsAsync(ct)).ToList();
			await _yandexPlaylistInitializer.InitializeAsync(yandexPlaylists, ct);

			var appPlaylists = await _appPlaylistProvider.GetAppPlaylistsAsync(ct);

			return appPlaylists.Concat(yandexPlaylists.Where(p => p.Type is PlaylistType.Custom or PlaylistType.Favorite));
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
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
			var slice = _tracksIds
				.Skip(_currentOffset)
				.Take(tracksPerBatch)
				.ToList();

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


