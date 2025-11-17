using YamBassPlayer.Enums;
using YamBassPlayer.Models;

using Yandex.Music.Api.Common;
using Yandex.Music.Api;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Library;
using Yandex.Music.Api.Models.Playlist;

namespace YamBassPlayer.Services;

public class TracksService : ITracksService
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly TrackInfoProvider _trackInfoProvider;

	private List<string> _tracksIds = new();

	private Playlist _currentPlaylist;

	private int _currentOffset = 0;

	public TracksService(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
		_trackInfoProvider = new TrackInfoProvider(api, storage);
	}

	public async Task SetPlaylist(Playlist playlist)
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

	private async Task SetCustomPlaylist(Playlist playlist)
	{
		List<YResponse<YPlaylist>>? yResponses = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);
		YResponse<YPlaylist>? yPlaylist = yResponses.FirstOrDefault(x => x.Result.Title == playlist.PlaylistName);
		List<string> tracksIds = yPlaylist.Result.Tracks.Select(x => x.Id).ToList();

		_tracksIds = tracksIds;
		_currentOffset = 0;
		_currentPlaylist = playlist;
	}

	private async Task SetFavorite(Playlist playlist)
	{
		YResponse<YLibraryTracks>? likedTracksResponse = await _api.Library.GetLikedTracksAsync(_storage);
		List<string> allTracks = likedTracksResponse.Result.Library.Tracks.Select(yLibraryTrack => yLibraryTrack.Id).ToList();
		_tracksIds = allTracks;
		_currentOffset = 0;
		_currentPlaylist = playlist;
	}

	private async Task SetPlaylistOfTheDay(Playlist playlist)
	{
		YResponse<YPlaylist>? yResponse = await _api.Playlist.OfTheDayAsync(_storage);
		YPlaylist? yResponseResult = yResponse.Result;
		_tracksIds = yResponseResult.Tracks.Select(yTrackContainer => yTrackContainer.Id).ToList();
		_currentOffset = 0;
		_currentPlaylist = playlist;
	}

	public async Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch)
	{
		var slice = _tracksIds
			.Skip(_currentOffset)
			.Take(tracksPerBatch)
			.ToList();

		_currentOffset += tracksPerBatch;

		List<Track> tracksResult = new List<Track>();
		foreach (string track in slice)
		{
			Track? trackInfoById = await _trackInfoProvider.GetTrackInfoById(track);
			if (trackInfoById == null)
				throw new NullReferenceException();

			tracksResult.Add(trackInfoById);
		}

		return tracksResult;
	}
}