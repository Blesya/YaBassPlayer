using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Views;

/// <summary>Окно «Моя волна»: обложка, текущий и следующий треки.</summary>
public interface IMyWaveView : IModalView
{
	(int Width, int Height) CoverSize { get; }

	void SetTrack(Track track);

	void SetListenCount(int count);

	void SetCover(CoverArt? art);

	void SetWaveDescription(string description);

	void SetNextTrackLabel(string? label);
}
