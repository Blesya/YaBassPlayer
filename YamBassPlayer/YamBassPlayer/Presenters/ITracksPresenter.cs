using System.Threading;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Presenters;

public interface ITracksPresenter
{
	event Action<Track>? OnTrackChosen;
	IPlaybackQueue PlaybackQueue { get; }

	/// <summary>
	/// Показывает треки плейлиста: сразу из локальной БД, метаданные — в фоне.
	/// Токен отменяет загрузку, если пользователь уже выбрал другой плейлист.
	/// </summary>
	Task LoadTracksFor(Playlist playlist, CancellationToken ct = default);
}
