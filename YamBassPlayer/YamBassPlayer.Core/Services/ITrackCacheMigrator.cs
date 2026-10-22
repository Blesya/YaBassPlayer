namespace YamBassPlayer.Services;

/// <summary>
/// Раскладывает старый плоский кэш треков по папкам источников.
/// </summary>
public interface ITrackCacheMigrator
{
	void Migrate();
}
