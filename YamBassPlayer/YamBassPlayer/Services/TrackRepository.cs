using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Playlist;

namespace YamBassPlayer.Services;

public class TrackRepository : ITrackRepository
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly TrackInfoProvider _trackInfoProvider;

	private List<string> _tracksIds = new();
	private Playlist _currentPlaylist;
	private int _currentOffset = 0;
	private readonly Dictionary<string, List<string>> _customPlaylistCache = new();

	public TrackRepository(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
		_trackInfoProvider = new TrackInfoProvider(api, storage);
	}

	public async Task<IEnumerable<Playlist>> GetPlaylists()
	{
		try
		{
			var yResponses = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);

			var liked = await _api.Library.GetLikedTracksAsync(_storage);
			int likedTracksCount = liked.Result.Library.Tracks.Count;

			List<Playlist> playlists =
			[
				new Playlist("Мои треки", PlaylistType.Favorite)
				{
					Description = "Треки, которые вам понравились",
					TrackCount = likedTracksCount
				}

			];

			foreach (YResponse<YPlaylist> yResponse in yResponses)
			{
				playlists.Add((new Playlist(yResponse.Result.Title, PlaylistType.Custom)
				{
					Description = yResponse.Result.Description,
					TrackCount = yResponse.Result.TrackCount
				}));
			}

			return playlists;
		}
		catch (Exception exception)
		{
			exception.Handle();
			return [];
		}
	}

	public async Task SetPlaylist(Playlist playlist)
	{
		try
		{
			switch (playlist.Type)
			{
				case PlaylistType.Favorite:
					await SetFavorite(playlist);
					break;
				case PlaylistType.PlaylistOfTheDaily:
					await SetPlaylistOfTheDay(playlist);
					break;
				case PlaylistType.Custom:
					await SetCustomPlaylist(playlist);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_currentPlaylist = playlist;
		}
		catch (Exception exception)
		{
			exception.Handle();
		}
	}

	private Task SetCustomPlaylist(Playlist playlist)
		=> SetPlaylistAsync(playlist, () => LoadCustomPlaylistAsync(playlist.PlaylistName));

	private Task SetFavorite(Playlist playlist)
		=> SetPlaylistAsync(playlist, LoadFavoritesAsync);

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
				if (_customPlaylistCache.TryGetValue(playlistName, out var cachedIds))
				{
					return cachedIds;
				}

				var playlists = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);
				var found = playlists.FirstOrDefault(x => x.Result.Title == playlistName)
				            ?? throw new InvalidOperationException($"Playlist '{playlistName}' not found");

				var trackIds = found.Result.Tracks.Select(t => t.Id).ToList();
				_customPlaylistCache[playlistName] = trackIds;

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
			var liked = await _api.Library.GetLikedTracksAsync(_storage);
			return liked.Result.Library.Tracks.Select(t => t.Id).ToList();
		});

	private Task<List<string>> LoadPlaylistOfTheDayAsync()
		=> Task.Run(async () =>
		{
			var day = await _api.Playlist.OfTheDayAsync(_storage);
			return day.Result.Tracks.Select(t => t.Id).ToList();
		});

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
}
