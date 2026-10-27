namespace YamBassPlayer.Models;

/// <summary>Итог обновления одного источника за проход.</summary>
public sealed record SourceRefreshResult(
	SourceId Source,
	bool Succeeded,
	int PlaylistCount,
	string? Error = null)
{
	public static SourceRefreshResult Skipped(SourceId source)
		=> new(source, true, 0);

	public static SourceRefreshResult Success(SourceId source, int playlistCount)
		=> new(source, true, playlistCount);

	public static SourceRefreshResult Failure(SourceId source, string error)
		=> new(source, false, 0, error);
}

/// <summary>
/// Слитый список плейлистов после обновления и состояние каждого источника.
/// Ошибка источника не выбрасывает его плейлисты из списка — они остаются из снимка.
/// </summary>
public sealed record PlaylistRefreshResult(
	IReadOnlyList<Playlist> Playlists,
	IReadOnlyList<SourceRefreshResult> Sources)
{
	public bool AnySourceFailed => Sources.Any(source => !source.Succeeded);
}
