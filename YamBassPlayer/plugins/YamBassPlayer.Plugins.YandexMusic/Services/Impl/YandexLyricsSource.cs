using Serilog;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Api.Models.Track;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Текст песни из Яндекс.Музыки. Кэширование — забота ядра (<c>LyricsService</c>),
/// плагин только загружает текст.
/// </summary>
public sealed class YandexLyricsSource : ILyricsSource
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;

	public YandexLyricsSource(YandexMusicApi api, AuthStorage storage)
	{
		_api = api;
		_storage = storage;
	}

	public async Task<string?> GetLyricsAsync(Track track)
	{
		try
		{
			var response = await _api.Track.GetSupplementAsync(_storage, track.Id);
			var lyrics = response?.Result?.Lyrics;
			return string.IsNullOrWhiteSpace(lyrics?.FullLyrics) ? null : lyrics.FullLyrics;
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось получить текст трека {TrackId}", track.Id);
			return null;
		}
	}
}
