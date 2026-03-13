using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IOnSameWavePresenter
{
	Task<Playlist?> ShowOnSameWaveAsync();
}
