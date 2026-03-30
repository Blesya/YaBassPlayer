using Terminal.Gui.Trees;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public class TrackRepository(
	IMusicSourceRegistry musicSourceRegistry,
	ITrackInfoProvider trackInfoProvider,
	string tracksFolder,
	IHistoryService historyService,
	ILocalFavoriteService localFavoriteService,
	IYandexFavoriteService yandexFavoriteService,
	ITrackRepositoryCache cache,
	ILocalLibraryService localLibraryService)
	: ITrackRepository
{
	private IMusicSource YandexSource => musicSourceRegistry.GetRequired("yandex");
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
		[PlaylistType.LocalFolder] = SetLocalFolderPlaylist,
		[PlaylistType.LocalArtist] = SetLocalArtistPlaylist,
		[PlaylistType.LocalAlbum] = SetLocalAlbumPlaylist,
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
			var yandexPlaylists = (await YandexSource.GetPlaylistsAsync()).ToList();

			// Load favorite track IDs for cache and service initialization
			var favPlaylist = yandexPlaylists.FirstOrDefault(p => p.Type == PlaylistType.Favorite);
			IEnumerable<Track> favoriteTracks = favPlaylist != null
				? await YandexSource.GetPlaylistTracksAsync(favPlaylist, 0, int.MaxValue)
				: [];
			string[] favoriteTrackIds = favoriteTracks.Select(t => t.Id).ToArray();
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

			foreach (var customPlaylist in yandexPlaylists.Where(p => p.Type == PlaylistType.Custom))
			{
				var tracks = (await YandexSource.GetPlaylistTracksAsync(customPlaylist, 0, int.MaxValue)).ToList();
				foreach (Track track in tracks)
				{
					await trackInfoProvider.SaveAsync(track);
				}

				List<string> trackIds = tracks.Select(t => t.Id).ToList();
				cache.SetCustomPlaylistIds(customPlaylist.PlaylistName, trackIds);

				playlists.Add(customPlaylist);
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

		var localSource = musicSourceRegistry.Get("local");
		if (localSource is not null)
		{
			var localPlaylists = (await localSource.GetPlaylistsAsync()).ToList();
			var folderPlaylists = localPlaylists.Where(p => p.Type == PlaylistType.LocalFolder).ToList();

			if (folderPlaylists.Count > 0)
			{
				var allLocalPlaylist = localPlaylists.FirstOrDefault(p => p.Type == PlaylistType.LocalSearch);

				// Put "all local" at the top of the group when available, then individual folders.
				List<Playlist> groupPlaylists = allLocalPlaylist is not null
					? [allLocalPlaylist, ..folderPlaylists]
					: folderPlaylists;

				// Build folder children as tree nodes.
				var folderItems = groupPlaylists
					.Select(PlaylistTreeItem.FromPlaylist)
					.Cast<ITreeNode>()
					.ToList();

				// Append a nested "Исполнители" sub-node when the local library has artists.
				var localArtists = await localLibraryService.GetLocalArtistsAsync();
				IList<ITreeNode> localMusicChildren = folderItems;
				if (localArtists.Count > 0)
				{
					var artistItems = new List<ITreeNode>();
					foreach (var (artistName, trackCount) in localArtists)
					{
						var artistPlaylist = new Playlist(artistName, PlaylistType.LocalArtist) { TrackCount = trackCount };
						var artistNode = PlaylistTreeItem.FromPlaylist(artistPlaylist);

						var albums = await localLibraryService.GetLocalAlbumsAsync(artistName);
						foreach (var (albumName, albumTrackCount) in albums)
						{
							// Encode artist+album in Description so the playlist setter can resolve tracks.
							var albumPlaylist = new Playlist(albumName, PlaylistType.LocalAlbum)
							{
								TrackCount = albumTrackCount,
								Description = $"{artistName}\n{albumName}"
							};
							artistNode.Children.Add(PlaylistTreeItem.FromPlaylist(albumPlaylist));
						}

						artistItems.Add(artistNode);
					}

					var artistsNode = new PlaylistTreeItem
					{
						Text = "Исполнители",
						Children = artistItems,
						Tag = "local-artists"
					};

					// Build "Альбомы" node — all albums across every artist.
					var allAlbums = await localLibraryService.GetAllLocalAlbumsAsync();
					ITreeNode? albumsNode = null;
					if (allAlbums.Count > 0)
					{
						var albumItems = allAlbums
							.Select(a => PlaylistTreeItem.FromPlaylist(
								new Playlist(a.albumName, PlaylistType.LocalAlbum)
								{
									TrackCount = a.trackCount,
									Description = $"\n{a.albumName}" // empty artist = all artists
								}))
							.Cast<ITreeNode>()
							.ToList();

						albumsNode = new PlaylistTreeItem
						{
							Text = "Альбомы",
							Children = albumItems,
							Tag = "local-albums"
						};
					}

					var extras = new List<ITreeNode> { artistsNode };
					if (albumsNode != null) extras.Add(albumsNode);
					localMusicChildren = [..folderItems, ..extras];
				}

				// Build the top-level "Локальная музыка" node with manually-composed children
				// so that both folder playlists and the artists sub-node are visible in the tree.
				var localMusicGroup = new PlaylistGroup("Локальная музыка", groupPlaylists, isExpanded: false);
				var localMusicNode = new PlaylistTreeItem
				{
					Text = localMusicGroup.Name,
					Group = localMusicGroup,
					Children = localMusicChildren,
					Tag = localMusicGroup
				};
				roots.Add(localMusicNode);
			}
		}

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

	private Task SetLocalFolderPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => LoadLocalFolderAsync(playlist));

	private Task SetLocalArtistPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => LoadLocalArtistAsync(playlist.PlaylistName));

	private async Task<List<string>> LoadLocalArtistAsync(string artistName)
	{
		var tracks = await localLibraryService.GetTracksByArtistAsync(artistName);
		return tracks.Select(t => t.Id).ToList();
	}

	private Task SetLocalAlbumPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => LoadLocalAlbumAsync(playlist));

	private async Task<List<string>> LoadLocalAlbumAsync(Playlist playlist)
	{
		// Description encodes "artistName\nalbumName" — empty artist means all artists ("Альбомы" node).
		var parts = playlist.Description?.Split('\n', 2);
		if (parts?.Length != 2)
			return [];

		string artistName = parts[0];
		string albumName = parts[1];

		IReadOnlyList<Track> tracks = string.IsNullOrEmpty(artistName)
			? await localLibraryService.GetTracksByAlbumTitleAsync(albumName)
			: await localLibraryService.GetTracksByAlbumAsync(artistName, albumName);

		return tracks.Select(t => t.Id).ToList();
	}

	private async Task<List<string>> LoadLocalFolderAsync(Playlist playlist)
	{
		var localSource = musicSourceRegistry.Get("local");
		if (localSource is null)
			return [];

		var tracks = await localSource.GetPlaylistTracksAsync(playlist, 0, int.MaxValue);
		return tracks.Select(t => t.Id).ToList();
	}

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

				var playlists = await YandexSource.GetPlaylistsAsync();
				var found = playlists.FirstOrDefault(p => p.PlaylistName == playlistName && p.Type == PlaylistType.Custom)
							?? throw new InvalidOperationException($"Playlist '{playlistName}' not found");

				var tracks = (await YandexSource.GetPlaylistTracksAsync(found, 0, int.MaxValue)).ToList();
				var trackIds = tracks.Select(t => t.Id).ToList();
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
		=> Task.Run(() => cache.FavoriteTrackIds.ToList());

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
			// PlaylistOfTheDaily is a Yandex-specific playlist type; routing through YandexSource
			var playlist = new Playlist(string.Empty, PlaylistType.PlaylistOfTheDaily);
			var tracks = await YandexSource.GetPlaylistTracksAsync(playlist, 0, int.MaxValue);
			return tracks.Select(t => t.Id).ToList();
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


