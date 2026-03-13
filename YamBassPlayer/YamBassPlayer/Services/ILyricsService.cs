namespace YamBassPlayer.Services;

public interface ILyricsService
{
	Task<string?> GetLyricsAsync(string trackId);
}
