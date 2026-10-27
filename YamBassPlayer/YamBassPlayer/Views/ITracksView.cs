using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ITracksView
{
	event Action<int>? OnTrackSelected;
	event Action<int>? OnCellActivated;
	event Action? NeedMoreTracks;
	void SetTracks(IEnumerable<Track> tracks, Func<Track, bool> isCached);
	void AddTracks(IEnumerable<Track> tracks, Func<Track, bool> isCached);
	void ClearTracks();
	void SetPlayingTrack(TrackKey? key);
	void SetFilter(string? filter);

	/// <summary>Выделить трек с указанным ключом и прокрутить список к нему.</summary>
	void SelectTrack(TrackKey key);

	/// <summary>Обновить состояние загрузки для трека с указанным ключом.</summary>
	void SetDownloadState(TrackKey key, TrackDownloadState state);

	/// <summary>Состояние списка: нет плейлиста, идёт загрузка или список готов.</summary>
	void SetState(TrackListState state);
}
