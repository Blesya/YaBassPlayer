using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface ITrackAboutPresenter
{
	/// <summary>Показать «О треке» для текущего трека (F7/команда about).</summary>
	void ShowCurrentTrackAbout();

	/// <summary>Показать «О треке» для указанного трека.</summary>
	void ShowTrackAbout(TrackKey key);

	/// <summary>Загрузить сведения о треке и показать окно; окно открывается одно.</summary>
	Task ShowTrackAboutAsync(TrackKey key, bool forceRefresh = false);
}
