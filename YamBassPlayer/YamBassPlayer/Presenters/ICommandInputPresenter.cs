using YamBassPlayer.Commands;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters;

public interface ICommandInputPresenter
{
	void HandleSubmitted(string raw);
}
