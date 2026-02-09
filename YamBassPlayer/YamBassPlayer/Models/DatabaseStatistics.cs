namespace YamBassPlayer.Models;

public class DatabaseStatistics
{
	public int TracksCount { get; init; }
	public int TotalListens { get; init; }
	public int UniqueListenedTracks { get; init; }
	public int CachedFilesCount { get; init; }
	public long CachedFilesSize { get; init; }
	public int LocalFavoritesCount { get; init; }
	public DateTime? FirstListenDate { get; init; }
	public DateTime? LastListenDate { get; init; }
	public long DatabaseFileSize { get; init; }
}
