using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ILyricsService
{
	Task<string?> GetLyricsAsync(Track track);
}
