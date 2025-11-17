using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Views;

using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
using Yandex.Music.Api.Models.Playlist;

namespace YamBassPlayer.Services;

public class PlaylistsService : IPlaylistsService
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;

	public PlaylistsService(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
	}

	public async Task<IEnumerable<Playlist>> GetPlaylists()
	{
		var yResponses = await _api.Playlist.GetPersonalPlaylistsAsync(_storage);

		List<Playlist> playlists = new List<Playlist>();

		playlists.Add(new Playlist("Мои треки", PlaylistType.Favorite));

		foreach (YResponse<YPlaylist> yResponse in yResponses)
		{
			playlists.Add((new Playlist(yResponse.Result.Title, PlaylistType.Custom)
			{
				Description = yResponse.Result.Description
			}));
		}

		return playlists;
	}
}