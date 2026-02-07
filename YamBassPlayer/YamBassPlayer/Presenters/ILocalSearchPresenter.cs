using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface ILocalSearchPresenter
{
	void ShowLocalSearchDialog();
	List<Track> GetSelectedTracks();
	bool WasCancelled();
}
