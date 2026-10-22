using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.UseCases;

/// <summary>
/// Прикладной сценарий переключения локального избранного.
/// Источник трека не важен: список хранится локально и общий для всех.
/// </summary>
public sealed class ToggleFavoriteUseCase
{
	private readonly ILocalFavoriteService _favorites;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IErrorHandler _errorHandler;

	public ToggleFavoriteUseCase(
		ILocalFavoriteService favorites,
		IPlayStatusPresenter playStatusPresenter,
		IErrorHandler errorHandler)
	{
		_favorites = favorites;
		_playStatusPresenter = playStatusPresenter;
		_errorHandler = errorHandler;
	}

	public async Task ExecuteAsync(TrackKey key)
	{
		Logging.LogBeforeCall();

		try
		{
			if (_favorites.IsTrackFavorite(key))
			{
				await _favorites.RemoveFromFavorites(key);
				_playStatusPresenter.SetPlayStatus("Удалён из избранного");
			}
			else
			{
				await _favorites.AddToFavorites(key);
				_playStatusPresenter.SetPlayStatus("Добавлен в избранное");
			}

			_playStatusPresenter.SetCurrentTrack(key);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			_playStatusPresenter.SetPlayStatus("Не удалось обновить избранное");
		}
	}
}
