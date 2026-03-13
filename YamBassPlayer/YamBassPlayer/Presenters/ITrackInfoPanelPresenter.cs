using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface ITrackInfoPanelPresenter
{
	void OnTrackSelected(Track track);
}
