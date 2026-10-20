using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ITrackAboutView : IModalView
{
	void SetInfo(TrackAboutInfo info);

	/// <summary>Показывает окно как обычный модальный диалог, не трогая панель управления.</summary>
	void Show();
}
