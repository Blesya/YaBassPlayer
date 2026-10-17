using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ITracksView
{
	event Action<int>? OnTrackSelected;
	event Action<int>? OnCellActivated;
	event Action? NeedMoreTracks;
	void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached);
	void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached);
	void ClearTracks();
	void SetPlayingTrackId(string? trackId);
	void SetFilter(string? filter);

	/// <summary>Обновляет состояние кэша одного трека без пересборки списка.</summary>
	void SetDownloadState(string trackId, TrackDownloadState state);
}