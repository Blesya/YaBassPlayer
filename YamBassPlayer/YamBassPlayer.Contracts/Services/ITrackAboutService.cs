using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackAboutService
{
	/// <summary>
	/// Свежая информация о треке. Кэш короткий и только в памяти: доступность
	/// и метки могут измениться, поэтому в БД это не сохраняется.
	/// </summary>
	Task<TrackAboutInfo> GetAsync(TrackKey key, bool forceRefresh = false);
}
