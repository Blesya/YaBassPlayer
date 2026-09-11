using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

/// <summary>Окно «Сейчас играет»: заголовок трека и визуализация спектра.</summary>
public interface INowPlayingView : IModalView
{
	/// <summary>Тип данных, который ожидает текущий рендерер спектра.</summary>
	SpectrumDataType SpectrumDataType { get; }

	void SetTrack(Track track);

	void SetSpectrumData(float[] data);

	void SetListenCount(int count);
}
