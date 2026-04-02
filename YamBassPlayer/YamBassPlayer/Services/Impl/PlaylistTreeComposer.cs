using Terminal.Gui.Trees;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class PlaylistTreeComposer(
	IMusicSourceRegistry musicSourceRegistry,
	ITrackInfoProvider trackInfoProvider,
	IHistoryService historyService,
	ILocalLibraryService localLibraryService)
	: IPlaylistTreeComposer
{
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

	/// <inheritdoc />
	public async Task<IReadOnlyList<PlaylistTreeItem>> ComposeAsync(IReadOnlyList<Playlist> playlists)
	{
		ArgumentNullException.ThrowIfNull(playlists);

		var roots = new List<PlaylistTreeItem>
		{
			await BuildSourcesRootAsync(playlists)
		};

		roots.AddRange(playlists
			.Where(IsApplicationRootPlaylist)
			.Select(PlaylistTreeItem.FromPlaylist));

		roots.Add(BuildTopByDayRoot());
		roots.Add(await BuildArtistsRootAsync());

		return roots;
	}

	private async Task<PlaylistTreeItem> BuildSourcesRootAsync(IReadOnlyList<Playlist> playlists)
	{
		var yandexSource = musicSourceRegistry.GetRequired("yandex");
		var localSource = musicSourceRegistry.GetRequired("local");

		var yandexPlaylists = playlists
			.Where(IsYandexSourcePlaylist)
			.ToList();

		var children = new List<ITreeNode>
		{
			PlaylistTreeItem.FromGroup(new PlaylistGroup(yandexSource.DisplayName, yandexPlaylists, isExpanded: false)),
			await BuildLocalMusicRootAsync(localSource)
		};

		var item = new PlaylistTreeItem
		{
			Label = "Источники",
			Children = children,
			Tag = "sources-root",
			IsExpandedByDefault = true
		};

		item.UpdateText();
		return item;
	}

	private async Task<PlaylistTreeItem> BuildLocalMusicRootAsync(IMusicSource localSource)
	{
		var localPlaylists = (await localSource.GetPlaylistsAsync()).ToList();
		var folderPlaylists = localPlaylists.Where(p => p.Type == PlaylistType.LocalFolder).ToList();
		var allLocalPlaylist = localPlaylists.FirstOrDefault(p => p.Type == PlaylistType.LocalSearch);

		List<Playlist> sourcePlaylists = allLocalPlaylist is not null
			? [allLocalPlaylist, ..folderPlaylists]
			: folderPlaylists;

		var children = new List<ITreeNode>(sourcePlaylists
			.Select(PlaylistTreeItem.FromPlaylist)
			.Cast<ITreeNode>());

		var localArtists = await localLibraryService.GetLocalArtistsAsync();
		if (localArtists.Count > 0)
		{
			children.Add(await BuildLocalArtistsRootAsync(localArtists));

			var localAlbumsRoot = await BuildLocalAlbumsRootAsync();
			if (localAlbumsRoot is not null)
			{
				children.Add(localAlbumsRoot);
			}
		}

		var localMusicGroup = new PlaylistGroup(localSource.DisplayName, sourcePlaylists, isExpanded: false);
		var item = new PlaylistTreeItem
		{
			Label = localMusicGroup.Name,
			Group = localMusicGroup,
			Children = children,
			Tag = localMusicGroup
		};

		item.UpdateText();
		return item;
	}

	private async Task<PlaylistTreeItem> BuildLocalArtistsRootAsync(
		IReadOnlyList<(string artistName, int trackCount)> localArtists)
	{
		var artistItems = new List<ITreeNode>();
		foreach (var (artistName, trackCount) in localArtists)
		{
			var artistPlaylist = new Playlist(artistName, PlaylistType.LocalArtist)
			{
				TrackCount = trackCount
			};
			var artistNode = PlaylistTreeItem.FromPlaylist(artistPlaylist);

			var albums = await localLibraryService.GetLocalAlbumsAsync(artistName);
			foreach (var (albumName, albumTrackCount) in albums)
			{
				var albumPlaylist = new Playlist(albumName, PlaylistType.LocalAlbum)
				{
					TrackCount = albumTrackCount,
					Description = $"{artistName}\n{albumName}"
				};
				artistNode.Children.Add(PlaylistTreeItem.FromPlaylist(albumPlaylist));
			}

			artistItems.Add(artistNode);
		}

		var item = new PlaylistTreeItem
		{
			Label = "Исполнители",
			Children = artistItems,
			Tag = "local-artists"
		};

		item.UpdateText();
		return item;
	}

	private async Task<PlaylistTreeItem?> BuildLocalAlbumsRootAsync()
	{
		var allAlbums = await localLibraryService.GetAllLocalAlbumsAsync();
		if (allAlbums.Count == 0)
		{
			return null;
		}

		var item = new PlaylistTreeItem
		{
			Label = "Альбомы",
			Children = allAlbums
				.Select(a => PlaylistTreeItem.FromPlaylist(
					new Playlist(a.albumName, PlaylistType.LocalAlbum)
					{
						TrackCount = a.trackCount,
						Description = $"\n{a.albumName}"
					}))
				.Cast<ITreeNode>()
				.ToList(),
			Tag = "local-albums"
		};

		item.UpdateText();
		return item;
	}

	private PlaylistTreeItem BuildTopByDayRoot()
	{
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

		return PlaylistTreeItem.FromGroup(new PlaylistGroup("Топ по дням", dayPlaylists, isExpanded: false));
	}

	private async Task<PlaylistTreeItem> BuildArtistsRootAsync()
	{
		var artists = await trackInfoProvider.GetArtistsWithTrackCountAsync();
		var artistPlaylists = artists
			.Select(a => new Playlist(a.artistName, PlaylistType.Artist)
			{
				TrackCount = a.trackCount
			})
			.ToList();

		return PlaylistTreeItem.FromGroup(new PlaylistGroup("Исполнители", artistPlaylists, isExpanded: false));
	}

	private static bool IsYandexSourcePlaylist(Playlist playlist)
		=> playlist.Type is PlaylistType.Favorite or PlaylistType.Custom or PlaylistType.PlaylistOfTheDaily;

	private static bool IsApplicationRootPlaylist(Playlist playlist)
		=> !IsYandexSourcePlaylist(playlist);
}
