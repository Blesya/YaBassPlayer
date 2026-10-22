using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IMyWavePresenter
{
	/// <summary>Запускает персональную "Мою волну".</summary>
	Task<Playlist?> StartMyWaveAsync();

	/// <summary>Запускает "Мою волну" по текущему играющему треку.</summary>
	Task<Playlist?> StartMyWaveFromTrackAsync(TrackKey key);

	/// <summary>Догружает следующий батч треков и добавляет их в очередь.</summary>
	Task FetchMoreTracksAsync();

	Task NotifyTrackStartedAsync(TrackKey key);
	Task NotifyTrackFinishedAsync(TrackKey key, double totalPlayedSeconds = 0);
	Task NotifyTrackSkippedAsync(TrackKey key, double totalPlayedSeconds = 0);
}
