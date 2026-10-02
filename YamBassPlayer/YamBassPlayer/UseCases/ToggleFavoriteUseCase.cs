using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.UseCases;

/// <summary>
/// Прикладной сценарий переключения избранного для трека в указанном источнике.
/// </summary>
public sealed class ToggleFavoriteUseCase
{
	private readonly ITrackFavoriteService _trackFavoriteService;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly ITrackSourceDetector _trackSourceDetector;
	private readonly IErrorHandler _errorHandler;

	public ToggleFavoriteUseCase(
		ITrackFavoriteService trackFavoriteService,
		IPlayStatusPresenter playStatusPresenter,
		ITrackSourceDetector trackSourceDetector,
		IErrorHandler errorHandler)
	{
		_trackFavoriteService = trackFavoriteService;
		_playStatusPresenter = playStatusPresenter;
		_trackSourceDetector = trackSourceDetector;
		_errorHandler = errorHandler;
	}

	public async Task ExecuteAsync(string sourceId, string trackId)
	{
		Logging.LogBeforeCall();

		try
		{
			if (!_trackFavoriteService.SupportsSource(sourceId))
			{
				_playStatusPresenter.SetPlayStatus("Источник избранного недоступен");
				return;
			}

			bool isFavorite = _trackFavoriteService.IsTrackFavorite(sourceId, trackId);
			if (isFavorite)
			{
				await _trackFavoriteService.RemoveFromFavorites(sourceId, trackId);
				_playStatusPresenter.SetPlayStatus("Удалён из избранного");
			}
			else
			{
				await _trackFavoriteService.AddToFavorites(sourceId, trackId);
				_playStatusPresenter.SetPlayStatus("Добавлен в избранное");
			}

			_playStatusPresenter.SetCurrentTrack(trackId, _trackSourceDetector.GetSourceId(trackId));
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			_playStatusPresenter.SetPlayStatus("Не удалось обновить избранное");
		}
	}
}
