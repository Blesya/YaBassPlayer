using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Library;
using Yandex.Music.Api.Models.Playlist;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public class TrackRepository(
	YandexMusicApi api,
	AuthStorage storage,
	ITrackInfoProvider trackInfoProvider,
	string tracksFolder,
	IHistoryService historyService,
	ILocalFavoriteService localFavoriteService,
	IYandexFavoriteService yandexFavoriteService,
	ITrackRepositoryCache cache)
	: ITrackRepository
{
	private List<string> _tracksIds = new();
	private Playlist? _currentPlaylist;
	private int _currentOffset = 0;
	private Dictionary<PlaylistType, Func<Playlist, Task>>? _playlistSetters;

	public PlaylistType? CurrentPlaylistType => _currentPlaylist?.Type;

	private Dictionary<PlaylistType, Func<Playlist, Task>> PlaylistSetters => _playlistSetters ??= new()
	{
		[PlaylistType.Favorite] = SetFavorite,
		[PlaylistType.PlaylistOfTheDaily] = SetPlaylistOfTheDay,
		[PlaylistType.Custom] = SetCustomPlaylist,
		[PlaylistType.Cached] = SetCachedPlaylist,
		[PlaylistType.Top10] = SetTop10Playlist,
		[PlaylistType.TopEvenings] = SetTopEveningsPlaylist,
		[PlaylistType.LocalFavorite] = SetLocalFavoritePlaylist,
		[PlaylistType.LocalSearch] = SetLocalSearchPlaylist,
		[PlaylistType.TopByDay] = SetTopByDayPlaylist,
		[PlaylistType.YandexSearch] = SetYandexSearchPlaylist,
		[PlaylistType.Artist] = SetArtistPlaylist,
		[PlaylistType.Queue] = SetQueuePlaylist,
		[PlaylistType.OnSameWave] = SetOnSameWavePlaylist,
		[PlaylistType.MyWave] = SetMyWavePlaylist
	};

	public async Task<IEnumerable<Playlist>> GetPlaylists()
	{
		try
		{
			IEnumerable<YResponse<YPlaylist>>? yResponses = await api.Playlist.GetPersonalPlaylistsAsync(storage);

			YResponse<YLibraryTracks>? liked = await api.Library.GetLikedTracksAsync(storage);
			string[] favoriteTrackIds = liked.Result.Library.Tracks.Select(x => x.Id).ToArray();
			cache.ReplaceFavoriteTrackIds(favoriteTrackIds);
			yandexFavoriteService.Initialize(favoriteTrackIds);
			yandexFavoriteService.OnFavoriteAdded += cache.InsertFavoriteTrackId;
			yandexFavoriteService.OnFavoriteRemoved += cache.RemoveFavoriteTrackId;
			int likedTracksCount = favoriteTrackIds.Length;

			var localFavoriteIds = await localFavoriteService.GetAllFavoriteTrackIds();

			List<Playlist> playlists =
			[
				new Playlist("Мои треки", PlaylistType.Favorite)
				{
					Description = "Треки, которые вам понравились",
					TrackCount = likedTracksCount
				},
				new Playlist("Локальное Избранное", PlaylistType.LocalFavorite)
				{
					Description = "Избранные треки (локально)",
					TrackCount = localFavoriteIds.Count
				},
				new Playlist("Загруженные", PlaylistType.Cached)
				{
					Description = "Треки из локального кеша",
					TrackCount = GetCachedTracksCount()
				},
				new Playlist("Топ 10", PlaylistType.Top10)
				{
					Description = "Топ 10 треков!",
					TrackCount = 10
				},
				new Playlist("Топ вечеров", PlaylistType.TopEvenings)
				{
					Description = "Топ треков с 16:00 до 24:00",
					TrackCount = 20
				},
			];

			foreach (YResponse<YPlaylist> yResponse in yResponses)
			{
				List<YTrackContainer>? yTrackContainers = yResponse.Result.Tracks;
				IEnumerable<Track> tracks = yTrackContainers.Select(yTrackContainer => yTrackContainer.Track.ToTrack());
				foreach (Track track in tracks)
				{
					await trackInfoProvider.SaveAsync(track);
				}

				List<string> trackIds = yTrackContainers.Select(t => t.Id).ToList();
				string? playlistTitle = yResponse.Result.Title;
				cache.SetCustomPlaylistIds(playlistTitle, trackIds);

				Playlist playlist = new Playlist(playlistTitle, PlaylistType.Custom)
				{
					Description = yResponse.Result.Description,
					TrackCount = yResponse.Result.TrackCount
				};

				playlists.Add(playlist);
			}

			return playlists;
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}

	private static readonly string[] DayNames =
	[
		"Понедельник", "Вторник", "Среда", "Четверг",
		"Пятница", "Суббота", "Воскресенье"
	];

	private static readonly DayOfWeek[] DaysOrder =
	[
		DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
		DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
	];

	public async Task<IEnumerable<PlaylistTreeItem>> GetPlaylistTree()
	{
		var playlists = (await GetPlaylists()).ToList();
		var roots = new List<PlaylistTreeItem>();

		foreach (var playlist in playlists)
		{
			roots.Add(PlaylistTreeItem.FromPlaylist(playlist));
		}

		var dayPlaylists = new List<Playlist>();
		for (int i = 0; i < DaysOrder.Length; i++)
		{
			var day = DaysOrder[i];
			var topTracks = historyService.GetTopTracksByDayOfWeek(day, 50);
			dayPlaylists.Add(new Playlist(DayNames[i], PlaylistType.TopByDay)
			{
				DayOfWeek = day,
				TrackCount = topTracks.Count
			});
		}

		var group = new PlaylistGroup("Топ по дням", dayPlaylists, isExpanded: false);
		roots.Add(PlaylistTreeItem.FromGroup(group));

		var artists = await trackInfoProvider.GetArtistsWithTrackCountAsync();
		var artistPlaylists = artists.Select(a => new Playlist(a.artistName, PlaylistType.Artist)
		{
			TrackCount = a.trackCount
		}).ToList();
		var artistGroup = new PlaylistGroup("Исполнители", artistPlaylists, isExpanded: false);
		roots.Add(PlaylistTreeItem.FromGroup(artistGroup));

		return roots;
	}

	public async Task SetPlaylist(Playlist playlist)
	{
		try
		{
			if (!PlaylistSetters.TryGetValue(playlist.Type, out var setPlaylist))
				throw new ArgumentOutOfRangeException(nameof(playlist.Type), playlist.Type, "Unsupported playlist type");

			await setPlaylist(playlist);

			_currentPlaylist = playlist;
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
	}

	private Task SetTop10Playlist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadTop10Playlist);

	private Task<List<string>> LoadTop10Playlist()
		=> Task.FromResult(
			historyService.GetTopTracks(10).Select(x => x.trackId).ToList());

	private Task SetTopEveningsPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadTopEveningsPlaylist);

	private Task SetTopByDayPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => LoadTopByDayPlaylist(playlist.DayOfWeek!.Value));

	private Task<List<string>> LoadTopByDayPlaylist(DayOfWeek day)
		=> Task.FromResult(
			historyService.GetTopTracksByDayOfWeek(day, 50).Select(x => x.trackId).ToList());

	private Task<List<string>> LoadTopEveningsPlaylist()
		=> Task.FromResult(
			historyService.GetTopEveningTracks(20).Select(x => x.trackId).ToList());

	private Task SetCustomPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => LoadCustomPlaylistAsync(playlist.PlaylistName));

	private Task SetCachedPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadCachedTracksAsync);

	private Task SetFavorite(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadFavoritesAsync);

	private Task SetLocalFavoritePlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadLocalFavoritesAsync);

	private Task SetLocalSearchPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadLocalSearchAsync);

	private Task SetYandexSearchPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadYandexSearchAsync);

	private Task SetArtistPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => trackInfoProvider.GetTrackIdsByArtistAsync(playlist.PlaylistName));

	private Task SetQueuePlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => Task.FromResult(cache.QueueTrackIds.ToList()));

	private Task SetOnSameWavePlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadOnSameWaveAsync);

	private Task<List<string>> LoadOnSameWaveAsync()
		=> Task.FromResult(cache.OnSameWaveTracks.Select(t => t.Id).ToList());

	private Task SetMyWavePlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadMyWaveAsync);

	private Task<List<string>> LoadMyWaveAsync()
		=> Task.FromResult(cache.MyWaveTracks.Select(t => t.Id).ToList());

	private Task SetPlaylistOfTheDay(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadPlaylistOfTheDayAsync);

	private async Task SetPlaylistAsync(Playlist playlist, Func<Task<List<string>>> loadTracks)
	{
		var trackIds = await loadTracks();

		_tracksIds = trackIds;
		_currentOffset = 0;
		_currentPlaylist = playlist;
	}

	private Task<List<string>> LoadCustomPlaylistAsync(string playlistName)
	{
		try
		{
			return Task.Run(async () =>
			{
				if (cache.TryGetCustomPlaylistIds(playlistName, out var cachedIds))
				{
					return cachedIds;
				}

				var playlists = await api.Playlist.GetPersonalPlaylistsAsync(storage);
				var found = playlists.FirstOrDefault(x => x.Result.Title == playlistName)
							?? throw new InvalidOperationException($"Playlist '{playlistName}' not found");

				var trackIds = found.Result.Tracks.Select(t => t.Id).ToList();
				cache.SetCustomPlaylistIds(playlistName, trackIds);

				return trackIds;
			});
		}
		catch (Exception exception)
		{
			exception.Handle();
			return new Task<List<string>>(() => new List<string>());
		}
	}

	private Task<List<string>> LoadFavoritesAsync()
		=> Task.Run(async () =>
		{
			//var liked = await _api.Library.GetLikedTracksAsync(_storage);
			//return liked.Result.Library.Tracks.Select(t => t.Id).ToList();
			return cache.FavoriteTrackIds.ToList();
		});

	private async Task<List<string>> LoadLocalFavoritesAsync()
	{
		return await localFavoriteService.GetAllFavoriteTrackIds();
	}

	private Task<List<string>> LoadLocalSearchAsync()
	{
		return Task.FromResult(cache.LocalSearchTrackIds.ToList());
	}

	private Task<List<string>> LoadYandexSearchAsync()
	{
		return Task.FromResult(cache.YandexSearchTrackIds.ToList());
	}

	private Task<List<string>> LoadPlaylistOfTheDayAsync()
		=> Task.Run(async () =>
		{
			var day = await api.Playlist.OfTheDayAsync(storage);
			return day.Result.Tracks.Select(t => t.Id).ToList();
		});

	private Task<List<string>> LoadCachedTracksAsync()
		=> Task.Run(() =>
		{
			if (!Directory.Exists(tracksFolder))
			{
				return new List<string>();
			}

			return Directory.GetFiles(tracksFolder, "*.mp3")
				.Select(filePath => new FileInfo(filePath))
				.OrderByDescending(fileInfo => fileInfo.CreationTime)
				.Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
				.Where(id => !string.IsNullOrEmpty(id))
				.ToList()!;
		});

	public int GetCachedTracksCount()
	{
		if (!Directory.Exists(tracksFolder))
		{
			return 0;
		}

		return Directory.GetFiles(tracksFolder, "*.mp3").Length;
	}

	public async Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch)
	{
		try
		{
			var slice = _tracksIds
				.Skip(_currentOffset)
				.Take(tracksPerBatch)
				.ToList();

			_currentOffset += tracksPerBatch;

			List<Track> tracksResult = new List<Track>();

			IEnumerable<Track> tracks = await trackInfoProvider.GetTracksInfoByIds(slice);

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

	public void UpdateLocalSearchCache(IEnumerable<Track> tracks)
		=> cache.ReplaceLocalSearchTracks(tracks);

	public void UpdateYandexSearchCache(IEnumerable<Track> tracks)
		=> cache.ReplaceYandexSearchTracks(tracks);

	public void UpdateQueueCache(IEnumerable<string> trackIds)
		=> cache.ReplaceQueueTrackIds(trackIds);

	public void UpdateOnSameWaveCache(IEnumerable<Track> tracks)
		=> cache.ReplaceOnSameWaveTracks(tracks);

	public void UpdateMyWaveCache(IEnumerable<Track> tracks)
	{
		cache.ReplaceMyWaveTracks(tracks);
		if (_currentPlaylist?.Type == PlaylistType.MyWave)
		{
			_tracksIds.Clear();
			_tracksIds.AddRange(cache.MyWaveTracks.Select(t => t.Id));
		}
	}

	public void AppendMyWaveCache(IEnumerable<Track> tracks)
	{
		var trackList = tracks.ToList();
		cache.AppendMyWaveTracks(trackList);
		if (_currentPlaylist?.Type == PlaylistType.MyWave)
			_tracksIds.AddRange(trackList.Select(t => t.Id));
	}

	public async Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount)
	{
		try
		{
			if (_currentPlaylist?.Type == PlaylistType.OnSameWave)
			{
				_currentOffset = cache.OnSameWaveTracks.Count;
				return cache.OnSameWaveTracks.ToList();
			}

			if (_currentPlaylist?.Type == PlaylistType.MyWave)
			{
				_currentOffset = cache.MyWaveTracks.Count;
				return cache.MyWaveTracks.ToList();
			}

			int cachedCount = await trackInfoProvider.CountCachedTracks(_tracksIds);

			int countToLoad = Math.Max(cachedCount, minCount);
			countToLoad = Math.Min(countToLoad, _tracksIds.Count);

			var idsToLoad = _tracksIds.Take(countToLoad).ToList();
			_currentOffset = countToLoad;

			return await trackInfoProvider.GetTracksInfoByIds(idsToLoad);
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}
}


