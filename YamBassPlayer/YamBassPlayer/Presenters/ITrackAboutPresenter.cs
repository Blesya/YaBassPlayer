namespace YamBassPlayer.Presenters;

public interface ITrackAboutPresenter
{
	/// <summary>Открыть «О треке» для текущего трека (F7/команда about).</summary>
	void ShowCurrentTrackAbout();

	/// <summary>Открыть «О треке» для указанного трека.</summary>
	void ShowTrackAbout(string trackId);

	/// <summary>Загрузить свежие данные и показать окно; ждёт закрытия окна.</summary>
	Task ShowTrackAboutAsync(string trackId, bool forceRefresh = false);
}
