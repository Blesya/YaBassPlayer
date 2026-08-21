namespace YamBassPlayer.Services;

/// <summary>
/// Фасад для рекомендации следующего трека на основе ML-модели.
/// По (prev, current) возвращает trackId следующего трека.
/// </summary>
public interface INextTrackPredictor
{
	/// <summary>Готова ли модель к работе (файлы модели загружены).</summary>
	bool IsReady { get; }

	/// <summary>
	/// Возвращает рекомендацию следующего трека. При неудаче в
	/// <see cref="NextTrackRecommendation.Message"/> содержится причина.
	/// Контекст: current -> prev -> prev2 (от самого свежего к более раннему).
	/// </summary>
	NextTrackRecommendation GetNext(string prev, string current, string prev2);
}
