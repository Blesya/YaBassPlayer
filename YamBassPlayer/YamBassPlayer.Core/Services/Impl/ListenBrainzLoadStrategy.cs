using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Ядровый плейлист ListenBrainz: стратегия не ходит в сеть, а отдаёт ключи,
/// которые положил сценарий показа. Так рекомендации живут по общим правилам
/// плейлистов ядра (<c>SourceId = null</c>) и не притворяются источником музыки.
/// </summary>
public sealed class ListenBrainzLoadStrategy : IPlaylistLoadStrategy
{
	/// <summary>Вид плейлиста; значение совпадает с <c>Playlist.Kind</c> в сценарии.</summary>
	public const string PlaylistKind = "listenbrainz";

	private IReadOnlyList<TrackKey> _trackKeys = [];

	public string Kind => PlaylistKind;

	public void SetTrackKeys(IEnumerable<TrackKey> trackKeys) => _trackKeys = trackKeys.ToList();

	public Task<List<TrackKey>> LoadTrackIdsAsync(Playlist playlist) => Task.FromResult(_trackKeys.ToList());
}
