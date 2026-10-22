using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Необязательная возможность источника: текст песни.
/// Ядро кэширует результат в своей БД, поэтому источник отвечает только за загрузку.
/// </summary>
public interface ILyricsSource
{
	Task<string?> GetLyricsAsync(Track track);
}
