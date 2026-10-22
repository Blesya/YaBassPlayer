using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IYandexRadioService
{
	bool IsSessionActive { get; }

	/// <summary>Запускает персональную "Мою волну".</summary>
	Task<bool> StartMyWaveAsync();

	/// <summary>Запускает "Мою волну" с затравкой по треку. При неудаче — падает на персональную волну.</summary>
	Task<bool> StartTrackWaveAsync(string seedTrackId);

	/// <summary>Получает следующий батч треков от активной станции.</summary>
	Task<(IReadOnlyList<string> trackIds, IReadOnlyList<Track> tracks)> FetchNextBatchAsync();

	Task SendTrackStartedAsync(string trackId);
	Task SendTrackFinishedAsync(string trackId, double totalPlayedSeconds = 0);
	Task SendTrackSkippedAsync(string trackId, double totalPlayedSeconds = 0);

	void Reset();
}
