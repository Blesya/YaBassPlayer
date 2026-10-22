using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IYandexSearchPresenter
{
	void ShowYandexSearchDialog();
	List<Track> GetSelectedTracks();
	bool WasCancelled();
}
