using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Лайк Яндекс.Музыки — плагинное действие: ходит в API и меняет «Мои треки».
/// Локальное избранное — отдельное действие <see cref="FavoriteTrackAction"/>.
/// </summary>
public sealed class YandexLikeTrackAction(IYandexFavoriteService favorites) : ITrackAction
{
    public string Id => "yandex-like";

    public event Action? Changed;

    public bool IsAvailable(Track track) => track.SourceId == YandexMusicSourceId.Value;

    public string GetTitle(Track track)
        => favorites.IsTrackFavorite(track.Id) ? "Яндекс −" : "Яндекс +";

    public async Task ExecuteAsync(Track track)
    {
        if (favorites.IsTrackFavorite(track.Id))
            await favorites.RemoveFromFavorites(track.Id);
        else
            await favorites.AddToFavorites(track.Id);

        Changed?.Invoke();
    }
}
