using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Track;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class LyricsService : ILyricsService
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;

	public LyricsService(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
	}

	public async Task<string?> GetLyricsAsync(Track track)
	{
		// Local tracks use file paths as IDs — the Yandex API cannot handle them
		if (track.SourceType == "local")
			return null;

		try
		{
			var response = await _api.Track.GetSupplementAsync(_storage, track.Id);
			var lyrics = response?.Result?.Lyrics;
			if (lyrics == null)
				return null;

			return string.IsNullOrWhiteSpace(lyrics.FullLyrics) ? null : lyrics.FullLyrics;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return null;
		}
	}
}
