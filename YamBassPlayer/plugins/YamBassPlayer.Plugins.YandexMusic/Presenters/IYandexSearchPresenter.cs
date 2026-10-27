using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IYandexSearchPresenter
{
	Task ShowYandexSearchDialogAsync();
	List<Track> GetSelectedTracks();
	bool WasCancelled();
}
