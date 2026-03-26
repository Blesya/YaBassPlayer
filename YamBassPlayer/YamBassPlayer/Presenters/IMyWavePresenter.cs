using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IMyWavePresenter
{
	/// <summary>Запускает персональную "Мою волну".</summary>
	Task<Playlist?> StartMyWaveAsync();

	/// <summary>Запускает "Мою волну" по текущему играющему треку.</summary>
	Task<Playlist?> StartMyWaveFromTrackAsync(string trackId);

	/// <summary>Догружает следующий батч треков и добавляет их в очередь.</summary>
	Task FetchMoreTracksAsync();

	Task NotifyTrackStartedAsync(string trackId);
	Task NotifyTrackFinishedAsync(string trackId, double totalPlayedSeconds = 0);
	Task NotifyTrackSkippedAsync(string trackId, double totalPlayedSeconds = 0);
}
