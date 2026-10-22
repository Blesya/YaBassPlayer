using Serilog;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Окно «О треке»: если источник умеет отдавать подробности
/// (<see cref="ITrackAboutSource"/>), спрашиваем его; иначе показываем
/// базовые поля трека из каталога. Ядро не знает, откуда пришёл трек.
/// </summary>
public sealed class TrackAboutService : ITrackAboutService
{
	private readonly IMusicSourceRegistry _sources;
	private readonly ITrackCatalog _trackCatalog;

	public TrackAboutService(IMusicSourceRegistry sources, ITrackCatalog trackCatalog)
	{
		_sources = sources;
		_trackCatalog = trackCatalog;
	}

	public async Task<TrackAboutInfo> GetAsync(TrackKey key, bool forceRefresh = false)
	{
		if (_sources.Get(key.SourceId) is ITrackAboutSource source)
			return await source.GetAboutAsync(key, forceRefresh);

		Track? track = null;
		try
		{
			track = await _trackCatalog.GetAsync(key);
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Не удалось прочитать базовую информацию о треке {TrackId}", key.TrackId);
		}

		return new TrackAboutInfo
		{
			TrackId = key.TrackId,
			Title = FirstNonEmpty(track?.Title, key.TrackId),
			Artist = FirstNonEmpty(track?.Artist, "Неизвестный исполнитель"),
			Album = track?.Album,
			IsAvailable = true,
		};
	}

	private static string FirstNonEmpty(params string?[] values)
	{
		return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
			?? string.Empty;
	}
}
